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
        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Bid amount must be greater than zero.");
        }

        if (tenantContext.IsPlatformAdmin)
        {
            throw new UnauthorizedAccessException("Platform admins are not allowed to place bids.");
        }

        if (tenantContext.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException("Tenant users are not allowed to place bids.");
        }

        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.Buyer);

        if (tenantContext is not ITenantContextWithActor actor || !actor.ActorId.HasValue)
        {
            throw new UnauthorizedAccessException("Bidder identity is required.");
        }

        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Compliance)
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

        if (listing.BuyNowPrice.HasValue)
        {
            throw new InvalidOperationException("Bids are only allowed for auction listings.");
        }

        var auction = await dbContext.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ListingId == listing.Id, cancellationToken);

        if (auction is null)
        {
            throw new InvalidOperationException("Auction not found.");
        }

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
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "place bids");
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.Buyer);

        if (amount <= 0)
        {
            throw new InvalidOperationException("Bid amount must be greater than zero.");
        }

        if (tenantContext is not ITenantContextWithActor actor || !actor.ActorId.HasValue)
        {
            // TODO: Replace with real authenticated user id.
            throw new UnauthorizedAccessException("Bidder identity is required.");
        }

        var auction = await dbContext.Auctions
            .FirstOrDefaultAsync(x => x.Id == auctionId, cancellationToken);

        if (auction is null)
        {
            throw new InvalidOperationException("Auction not found.");
        }

        if (auction.IsClosed)
        {
            throw new InvalidOperationException("Auction is closed.");
        }

        var now = DateTimeOffset.UtcNow;
        if (auction.StartsAt.HasValue && auction.StartsAt.Value > now)
        {
            throw new InvalidOperationException("Auction is not open yet.");
        }

        if (auction.EndsAt.HasValue && auction.EndsAt.Value <= now)
        {
            throw new InvalidOperationException("Auction has ended.");
        }

        var listing = await dbContext.Listings
            .Include(x => x.Compliance)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == auction.ListingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        if (!listing.IsActive || listing.Compliance?.Status != ListingComplianceStatus.Approved)
        {
            throw new InvalidOperationException("Listing is not approved for bidding.");
        }

        var currentPrice = await GetCurrentPriceAsync(auctionId, cancellationToken);
        var minimumAllowed = (currentPrice ?? auction.StartingPrice) + MinimumIncrement;

        if (amount < minimumAllowed)
        {
            throw new InvalidOperationException($"Bid must be at least {minimumAllowed}.");
        }

        var entity = new Bid
        {
            TenantId = auction.TenantId,
            AuctionId = auction.Id,
            BidderId = actor.ActorId.Value,
            Amount = amount,
            CurrencyCode = "USD",
            PlacedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Bids.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new BidDto(entity.Id, entity.AuctionId, entity.BidderId, entity.Amount, entity.CurrencyCode, entity.PlacedAt);
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
