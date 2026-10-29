using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class BidService(IApplicationDbContext dbContext, ITenantContext tenantContext, TimeProvider clock) : IBidService
{
    public async Task PlaceBidAsync(CreateBidRequest request, CancellationToken cancellationToken) =>
        _ = await PlaceAsync(request, cancellationToken);

    private async Task<BidDto> PlaceAsync(CreateBidRequest request, CancellationToken ct)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "place bids");
        ServiceAuthorization.EnsureTenantUser(tenantContext);
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.Buyer);
        var actor = (tenantContext as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException();
        if (!TimedAuctionRules.Money(request.Amount)) throw new ListingValidationException("Use a positive bid up to 999999999 with at most two decimal places.");
        var listing = await dbContext.Listings.Include(x => x.Compliance).SingleOrDefaultAsync(x => x.Id == request.ListingId, ct)
            ?? throw new KeyNotFoundException("Listing not found.");
        TimedAuctionRules.Approved(listing);
        if (listing.SellerId == actor) throw new ListingValidationException("You cannot bid on your own listing.");
        var auctions = await dbContext.Auctions.Where(x => x.ListingId == listing.Id && x.TenantId == listing.TenantId).Take(2).ToListAsync(ct);
        if (auctions.Count != 1) throw new ListingEditConflictException("A single configured auction is required.");
        var auction = auctions[0];
        var now = clock.GetUtcNow();
        if (auction.RulesVersion != 1 || auction.Type != AuctionType.Timed || auction.IsSealedBid || auction.IsClosed
            || auction.State != AuctionState.Scheduled || auction.StartsAt is null || auction.EndsAt is null
            || now < auction.StartsAt || now >= auction.EndsAt)
            throw new ListingEditConflictException("This auction is not open for bidding.");
        var minimum = auction.LeadingAmount is { } top ? top + auction.MinimumIncrement : auction.StartingPrice;
        if (request.Amount < minimum) throw new ListingValidationException($"The minimum bid is {minimum:0.00} {listing.CurrencyCode}. Reload the auction.");
        var bid = new Bid { TenantId = auction.TenantId, AuctionId = auction.Id, BidderId = actor,
            BidNumber = auction.BidCount + 1, Amount = request.Amount, CurrencyCode = listing.CurrencyCode,
            PlacedAt = now, CreatedAt = now, UpdatedAt = now };
        dbContext.Bids.Add(bid);
        auction.LeadingBidId = bid.Id; auction.LeadingBidderId = actor; auction.LeadingAmount = bid.Amount; auction.BidCount++;
        TimedAuctionRules.Touch(auction, listing, now);
        // Bid, leader and both concurrency tokens commit together. Losing requests must reload, never auto-raise a bid.
        await dbContext.SaveChangesAsync(ct);
        return new(bid.Id, bid.AuctionId, bid.BidderId, bid.Amount, bid.CurrencyCode, bid.PlacedAt);
    }
    public async Task<BidDto> PlaceBidAsync(Guid auctionId, decimal amount, CancellationToken cancellationToken = default)
    {
        var auction = await dbContext.Auctions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == auctionId, cancellationToken)
            ?? throw new KeyNotFoundException("Auction not found.");
        return await PlaceAsync(new(auction.ListingId, amount), cancellationToken);
    }
    private async Task<Auction> VisibleAsync(Guid id, CancellationToken ct)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "access tenant bidding");
        var auction = await dbContext.Auctions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Auction not found.");
        var listing = await dbContext.Listings.AsNoTracking().Include(x => x.Compliance).SingleOrDefaultAsync(x => x.Id == auction.ListingId, ct)
            ?? throw new KeyNotFoundException("Listing not found.");
        TimedAuctionRules.Approved(listing);
        if (auction.RulesVersion != 1 || auction.IsSealedBid || auction.Type != AuctionType.Timed || auction.State == AuctionState.Draft)
            throw new KeyNotFoundException("Auction not available.");
        return auction;
    }
    public async Task<PagedResult<BidDto>> GetBidsForAuctionAsync(Guid auctionId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var auction = await VisibleAsync(auctionId, cancellationToken);
        skip = Math.Max(0, skip); take = Math.Clamp(take, 1, 100);
        var query = dbContext.Bids.AsNoTracking().Where(x => x.AuctionId == auctionId && x.TenantId == auction.TenantId);
        var actor = (tenantContext as ITenantContextWithActor)?.ActorId;
        var manager = tenantContext is ITenantContextWithRoles roles && roles.Roles.Contains(FaunexRoles.TenantAdmin);
        if (!manager) query = query.Where(x => x.BidderId == actor);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.BidNumber).Skip(skip).Take(take)
            .Select(x => new BidDto(x.Id, x.AuctionId, x.BidderId, x.Amount, x.CurrencyCode, x.PlacedAt)).ToListAsync(cancellationToken);
        return new(items, total);
    }
    public async Task<decimal?> GetCurrentPriceAsync(Guid auctionId, CancellationToken cancellationToken = default) =>
        (await VisibleAsync(auctionId, cancellationToken)).LeadingAmount;
}
