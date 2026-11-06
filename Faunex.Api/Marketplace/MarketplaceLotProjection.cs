using Faunex.Application.DTOs;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Marketplace;

public sealed class MarketplaceLotProjection(ApplicationDbContext db)
{
    public IQueryable<Listing> Visible(Guid tenant, bool includeReserved = false) => db.Listings.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == tenant && x.IsActive
        && db.Tenants.Any(t => t.Id == tenant && t.IsActive) && x.Compliance != null && x.Compliance.TenantId == tenant && x.Compliance.Status == ListingComplianceStatus.Approved
        && (includeReserved || !db.AuctionOrders.IgnoreQueryFilters().Any(o => o.TenantId == tenant && o.ListingId == x.Id && o.AuctionId == null && o.ReleasedAt == null)));

    public IQueryable<Auction> PublishedAuctions(Guid tenant) => db.Auctions.IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.TenantId == tenant && x.RulesVersion == 1 && x.Type == AuctionType.Timed && !x.IsSealedBid && x.State != AuctionState.Draft
            && x.Listing != null && x.Listing.TenantId == tenant && x.CurrencyCode == x.Listing.CurrencyCode && x.Listing.IsActive && x.Listing.Compliance != null
            && x.Listing.Compliance.TenantId == tenant && x.Listing.Compliance.Status == ListingComplianceStatus.Approved);

    public async Task<IReadOnlyList<MarketplaceLotDto>> ReadAsync(Guid tenant, IReadOnlyList<Guid> orderedIds, DateTimeOffset now, CancellationToken ct)
    {
        if (orderedIds.Count == 0) return [];
        if (orderedIds.Count > 48) throw new ArgumentOutOfRangeException(nameof(orderedIds));
        var rows = await Visible(tenant).Where(x => orderedIds.Contains(x.Id)).Select(x => new MarketplaceLotDto(x.Id, x.Title,
            x.BirdDetails != null ? "bird" : x.LivestockDetails != null ? "livestock" : x.GameAnimalDetails != null ? "game" : x.PoultryDetails != null ? "poultry" : "unknown",
            x.Location, x.Quantity, x.StartingPrice, x.BuyNowPrice, x.CurrencyCode, null, null)).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var photos = await db.ListingFiles.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenant && ids.Contains(x.ListingId) && x.IsPhoto && x.DeletedAt == null)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => new { x.ListingId, x.Id }).ToListAsync(ct);
        var auctions = await PublishedAuctions(tenant).Where(x => ids.Contains(x.ListingId)).ToListAsync(ct);
        var byId = rows.ToDictionary(x => x.Id);
        var photoIds = photos.GroupBy(x => x.ListingId).ToDictionary(x => x.Key, x => (Guid?)x.First().Id);
        var publicAuctions = auctions.GroupBy(x => x.ListingId).ToDictionary(x => x.Key, x => x.Count() == 1 ? TimedAuctionRules.Public(x.Single(), now) : null);
        return orderedIds.Where(byId.ContainsKey).Select(id => byId[id] with { PhotoId = photoIds.GetValueOrDefault(id), Auction = publicAuctions.GetValueOrDefault(id) }).ToList();
    }
}
