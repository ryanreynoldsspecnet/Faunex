using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class BidService(IApplicationDbContext dbContext, ITenantContext tenantContext) : IBidService
{
    private const decimal MinimumIncrement = 10m;

    public async Task PlaceBidAsync(CreateBidRequest request, CancellationToken cancellationToken)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "place bids");
        ServiceAuthorization.EnsureTenantUser(tenantContext);
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.Buyer);

        if (tenantContext is not ITenantContextWithActor actor || !actor.ActorId.HasValue)
        {
            throw new UnauthorizedAccessException("Authenticated user context is required.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Bid amount must be greater than 0.");
        }

        var listing = await dbContext.Listings
            .Include(x => x.Compliance)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ListingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        if (!listing.IsActive)
        {
            throw new InvalidOperationException("Listing is not active.");
        }

        if (listing.Compliance?.Status != ListingComplianceStatus.Approved)
        {
            throw new InvalidOperationException("Listing is not approved for bidding.");
        }

        // Bidding is only supported for auction listings (BuyNowPrice indicates fixed-price listing).
        if (listing.BuyNowPrice.HasValue)
        {
            throw new InvalidOperationException("Bids are only allowed for auction listings.");
        }

        // Canonical bid placement path is listing-based:
        // - Resolve the listing's auction.
        // - Enforce auction lifecycle rules (only open auctions accept bids).
        // Auction lifecycle is modeled implicitly (v1): IsClosed == true means bidding MUST be rejected.
        var auction = await dbContext.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ListingId == listing.Id, cancellationToken);

        if (auction is null)
        {
            throw new InvalidOperationException("Auction not found.");
        }

        // Single rule for correctness: only open auctions accept bids.
        // "Open" is represented by IsClosed == false. "Closed" is IsClosed == true.
        if (auction.IsClosed)
        {
            throw new InvalidOperationException("Auction is closed.");
        }

        var entity = new Bid
        {
            TenantId = auction.TenantId,
            AuctionId = auction.Id,
            BidderId = actor.ActorId.Value,
            Amount = request.Amount,
            CurrencyCode = listing.CurrencyCode,
            PlacedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Bids.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<BidDto> PlaceBidAsync(Guid auctionId, decimal amount, CancellationToken cancellationToken = default)
    {
        // Compatibility wrapper (HTTP /api/auctions/{auctionId}/bids):
        // Resolve auction -> listingId, then delegate to canonical listing-based bid placement.
        var auction = await dbContext.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == auctionId, cancellationToken);

        if (auction is null)
        {
            throw new InvalidOperationException("Auction not found.");
        }

        await PlaceBidAsync(new CreateBidRequest(auction.ListingId, amount), cancellationToken);

        // Return the newest bid for this auction (response shape preserved).
        var bid = await dbContext.Bids
            .AsNoTracking()
            .Where(x => x.AuctionId == auctionId)
            .OrderByDescending(x => x.PlacedAt)
            .Select(x => new BidDto(x.Id, x.AuctionId, x.BidderId, x.Amount, x.CurrencyCode, x.PlacedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return bid ?? throw new InvalidOperationException("Bid could not be created.");
    }

    public async Task<PagedResult<BidDto>> GetBidsForAuctionAsync(Guid auctionId, int skip, int take, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);

        take = take <= 0 ? 20 : Math.Min(take, 200);
        skip = skip < 0 ? 0 : skip;

        var auction = await dbContext.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == auctionId, cancellationToken);

        if (auction is null)
        {
            throw new InvalidOperationException("Auction not found.");
        }

        // Buyer visibility: only allow viewing bids if the listing is approved/active OR tenant admin/seller.
        // TODO: Extend with additional roles/policies later.
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == auction.ListingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        if (IsBuyer(tenantContext) && (!listing.IsActive || listing.Compliance?.Status != ListingComplianceStatus.Approved))
        {
            throw new UnauthorizedAccessException("Bids are not available for this listing.");
        }

        var q = dbContext.Bids
            .AsNoTracking()
            .Where(x => x.AuctionId == auctionId);

        var total = await q.CountAsync(cancellationToken);

        // Bid history ordering: newest-first by PlacedAt.
        var items = await q
            .OrderByDescending(x => x.PlacedAt)
            .Skip(skip)
            .Take(take)
            .Select(x => new BidDto(x.Id, x.AuctionId, x.BidderId, x.Amount, x.CurrencyCode, x.PlacedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<BidDto>(items, total);
    }

    public async Task<decimal?> GetCurrentPriceAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);

        // Current price rule (v1): highest bid amount wins.
        // Note: This is a "highest-bid" model, not "most-recent bid".
        var top = await dbContext.Bids
            .AsNoTracking()
            .Where(x => x.AuctionId == auctionId)
            .OrderByDescending(x => x.Amount)
            .Select(x => (decimal?)x.Amount)
            .FirstOrDefaultAsync(cancellationToken);

        return top;
    }

    private static bool IsBuyer(ITenantContext tenantContext)
    {
        if (tenantContext is ITenantContextWithRoles withRoles)
        {
            return withRoles.Roles.Contains(FaunexRoles.Buyer);
        }

        return false;
    }
}
