using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class AuctionService(IApplicationDbContext dbContext, ITenantContext tenantContext) : IAuctionService
{
    public async Task<AuctionDto?> GetByIdAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);

        var entity = await dbContext.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == auctionId, cancellationToken);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<IReadOnlyList<AuctionDto>> GetByListingIdAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);

        var entities = await dbContext.Auctions
            .AsNoTracking()
            .Where(x => x.ListingId == listingId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<Guid> CreateAsync(AuctionDto auction, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "create auctions");
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.TenantAdmin, FaunexRoles.Seller);

        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == auction.ListingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        // Seller can create auctions only for their own listings.
        ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, listing.SellerId);

        var entity = new Auction
        {
            Id = auction.Id == Guid.Empty ? Guid.NewGuid() : auction.Id,
            TenantId = listing.TenantId,
            ListingId = auction.ListingId,
            Type = auction.Type,
            StartsAt = auction.StartsAt,
            EndsAt = auction.EndsAt,
            StartingPrice = auction.StartingPrice,
            ReservePrice = auction.ReservePrice,
            BuyNowPrice = auction.BuyNowPrice,
            IsSealedBid = auction.IsSealedBid,
            IsClosed = auction.IsClosed
        };

        dbContext.Auctions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task UpdateAsync(AuctionDto auction, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "update auctions");
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.TenantAdmin, FaunexRoles.Seller);

        var entity = await dbContext.Auctions
            .FirstOrDefaultAsync(x => x.Id == auction.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Auction not found.");
        }

        // Sellers can update auctions only for their own listings.
        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entity.ListingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, listing.SellerId);

        entity.Type = auction.Type;
        entity.StartsAt = auction.StartsAt;
        entity.EndsAt = auction.EndsAt;
        entity.StartingPrice = auction.StartingPrice;
        entity.ReservePrice = auction.ReservePrice;
        entity.BuyNowPrice = auction.BuyNowPrice;
        entity.IsSealedBid = auction.IsSealedBid;

        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task OpenAuctionAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "open auctions");
        ServiceAuthorization.EnsureTenantAdmin(tenantContext);

        var entity = await dbContext.Auctions
            .FirstOrDefaultAsync(x => x.Id == auctionId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Auction not found.");
        }

        // Auction lifecycle (v1):
        // - "Open" is represented by IsClosed == false.
        // - "Closed" is represented by IsClosed == true.
        // - StartsAt/EndsAt are informational timestamps; IsClosed is authoritative for open/close.
        // Bidding rules elsewhere MUST treat IsClosed == true as non-biddable.
        if (!entity.IsClosed)
        {
            // Already open (v1 state uses IsClosed + timestamps).
            return;
        }

        // Optional recommended validation: cannot open without approved listing.
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == entity.ListingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        if (!listing.IsActive || listing.Compliance?.Status != ListingComplianceStatus.Approved)
        {
            throw new InvalidOperationException("Listing is not approved for auction.");
        }

        entity.IsClosed = false;
        entity.StartsAt ??= DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "close auctions");
        ServiceAuthorization.EnsureTenantAdmin(tenantContext);

        var entity = await dbContext.Auctions
            .FirstOrDefaultAsync(x => x.Id == auctionId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Auction not found.");
        }

        // Auction lifecycle (v1): once IsClosed is set to true, the auction is no longer open and MUST reject bids.
        if (entity.IsClosed)
        {
            throw new InvalidOperationException("Auction is already closed.");
        }

        entity.IsClosed = true;
        entity.EndsAt ??= DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // TODO: Determine winning bid when bidding rules are finalized.

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuctionDto MapToDto(Auction entity) =>
        new(
            entity.Id,
            entity.ListingId,
            entity.Type,
            entity.StartsAt,
            entity.EndsAt,
            entity.StartingPrice,
            entity.ReservePrice,
            entity.BuyNowPrice,
            entity.IsSealedBid,
            entity.IsClosed
        );
}
