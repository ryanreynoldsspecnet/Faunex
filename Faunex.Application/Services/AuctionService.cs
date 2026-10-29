using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class AuctionService(IApplicationDbContext dbContext, ITenantContext tenantContext, TimeProvider clock) : IAuctionService
{
    private void Manager()
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "manage auctions");
        ServiceAuthorization.EnsureTenantAdminOrSeller(tenantContext);
    }
    private void Administrator() { Manager(); ServiceAuthorization.EnsureTenantAdmin(tenantContext); }
    private async Task<Listing> ListingAsync(Guid id, CancellationToken ct)
    {
        var listing = await dbContext.Listings.Include(x => x.Compliance).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Listing not found.");
        ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, listing.SellerId);
        return listing;
    }
    private async Task<Auction> AuctionAsync(Guid id, CancellationToken ct) =>
        await dbContext.Auctions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Auction not found.");

    public async Task<AuctionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Manager();
        var auction = await dbContext.Auctions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (auction is null) return null;
        var listing = await ListingAsync(auction.ListingId, cancellationToken);
        return Map(auction, listing);
    }
    public async Task<IReadOnlyList<AuctionDto>> GetByListingIdAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        Manager();
        var listing = await ListingAsync(listingId, cancellationToken);
        var auctions = await dbContext.Auctions.AsNoTracking().Where(x => x.ListingId == listingId).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        return auctions.Select(x => Map(x, listing)).ToList();
    }
    private void Validate(AuctionDto input, Listing listing)
    {
        TimedAuctionRules.Approved(listing);
        if (input.ListingRevision is null) throw new ListingValidationException("Reload the listing and send its revision.");
        if (input.ListingRevision != listing.Revision) throw new ListingEditConflictException("The listing changed. Reload before saving auction terms.");
        if (input.Type != AuctionType.Timed || input.IsSealedBid || input.BuyNowPrice.HasValue)
            throw new ListingValidationException("The pilot supports timed auctions only.");
        if (!TimedAuctionRules.Money(input.StartingPrice) || input.StartingPrice != listing.StartingPrice
            || !TimedAuctionRules.Money(input.MinimumIncrement)
            || (input.ReservePrice is { } reserve && (!TimedAuctionRules.Money(reserve) || reserve < input.StartingPrice)))
            throw new ListingValidationException("Use the approved starting price, a positive two-decimal increment, and an optional reserve at or above the start price.");
        if (input.StartsAt is null || input.EndsAt is null || input.StartsAt < clock.GetUtcNow()
            || input.EndsAt - input.StartsAt < TimeSpan.FromMinutes(1) || input.EndsAt - input.StartsAt > TimeSpan.FromDays(30))
            throw new ListingValidationException("Choose a future start and an end between one minute and 30 days after it.");
    }
    private static void Terms(Auction entity, AuctionDto input)
    {
        entity.Type = AuctionType.Timed; entity.RulesVersion = 1; entity.State = AuctionState.Draft; entity.IsClosed = true;
        entity.StartsAt = input.StartsAt!.Value.ToUniversalTime(); entity.EndsAt = input.EndsAt!.Value.ToUniversalTime();
        entity.StartingPrice = input.StartingPrice; entity.ReservePrice = input.ReservePrice; entity.MinimumIncrement = input.MinimumIncrement;
        entity.IsSealedBid = false; entity.BuyNowPrice = null;
    }
    public async Task<Guid> CreateAsync(AuctionDto input, CancellationToken cancellationToken = default)
    {
        Manager();
        var listing = await ListingAsync(input.ListingId, cancellationToken);
        Validate(input, listing);
        if (await dbContext.Auctions.AnyAsync(x => x.ListingId == listing.Id, cancellationToken))
            throw new ListingEditConflictException("This listing already has an auction. Reload its workspace.");
        var auction = new Auction { TenantId = listing.TenantId, ListingId = listing.Id };
        Terms(auction, input); dbContext.Auctions.Add(auction);
        // The listing revision serializes setup against duplicate setup, file edits and unpublication.
        TimedAuctionRules.Touch(auction, listing, clock.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return auction.Id;
    }
    public async Task UpdateAsync(AuctionDto input, CancellationToken cancellationToken = default)
    {
        Manager();
        var auction = await AuctionAsync(input.Id, cancellationToken);
        var listing = await ListingAsync(auction.ListingId, cancellationToken);
        if (input.ListingId != listing.Id) throw new ListingValidationException("Auction ownership cannot be changed.");
        TimedAuctionRules.Revision(auction, input.Revision);
        if ((auction.RulesVersion == 1 && auction.State != AuctionState.Draft) || auction.Outcome != null
            || await dbContext.Bids.AnyAsync(x => x.AuctionId == auction.Id, cancellationToken))
            throw new ListingEditConflictException("Published terms and auctions with bids cannot be edited.");
        Validate(input, listing); Terms(auction, input);
        TimedAuctionRules.Touch(auction, listing, clock.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task OpenAuctionAsync(Guid id, CancellationToken cancellationToken = default, Guid? revision = null)
    {
        Administrator();
        var auction = await AuctionAsync(id, cancellationToken);
        var listing = await ListingAsync(auction.ListingId, cancellationToken);
        TimedAuctionRules.Revision(auction, revision); TimedAuctionRules.Approved(listing);
        if (auction.RulesVersion != 1 || auction.State != AuctionState.Draft)
            throw new ListingEditConflictException("Only a configured draft auction can be published.");
        Validate(Map(auction, listing), listing);
        auction.State = AuctionState.Scheduled; auction.IsClosed = false; TimedAuctionRules.Touch(auction, listing, clock.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task CloseAsync(Guid id, CancellationToken cancellationToken = default, Guid? revision = null)
    {
        Administrator();
        var auction = await AuctionAsync(id, cancellationToken);
        var listing = await ListingAsync(auction.ListingId, cancellationToken);
        if (auction.State == AuctionState.Closed && auction.RulesVersion == 1) return;
        TimedAuctionRules.Revision(auction, revision);
        await AuctionSettlement.CompleteAsync(dbContext, auction, listing, clock.GetUtcNow(), cancellationToken);
    }
    public async Task CancelAsync(Guid id, Guid revision, string reason, CancellationToken cancellationToken = default)
    {
        Administrator();
        var auction = await AuctionAsync(id, cancellationToken);
        var listing = await ListingAsync(auction.ListingId, cancellationToken);
        TimedAuctionRules.Revision(auction, revision);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) throw new ListingValidationException("Provide a cancellation reason of up to 500 characters.");
        if (auction.RulesVersion != 1 || auction.State is not (AuctionState.Draft or AuctionState.Scheduled)
            || (auction.State == AuctionState.Scheduled && auction.StartsAt <= clock.GetUtcNow())
            || await dbContext.Bids.AnyAsync(x => x.AuctionId == id, cancellationToken))
            throw new ListingEditConflictException("Only an unstarted auction without bids can be cancelled.");
        auction.State = AuctionState.Cancelled; auction.IsClosed = true; auction.Outcome = "Cancelled";
        auction.CancellationReason = reason.Trim(); auction.ClosedAt = clock.GetUtcNow();
        TimedAuctionRules.Touch(auction, listing, clock.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    private AuctionDto Map(Auction a, Listing l) => new(a.Id, a.ListingId, a.Type, a.StartsAt, a.EndsAt, a.StartingPrice,
        a.ReservePrice, a.BuyNowPrice, a.IsSealedBid, a.IsClosed, a.MinimumIncrement, a.Revision, l.Revision,
        TimedAuctionRules.State(a, clock.GetUtcNow()), a.LeadingAmount, a.Outcome, a.WinnerId, a.ClosedAt);
}
