using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class ListingBrowseService(IApplicationDbContext dbContext) : IListingBrowseService
{
    public async Task<IReadOnlyList<ListingDto>> BrowseApprovedListingsAsync(CancellationToken cancellationToken)
    {
        var approvedComplianceByListingId = dbContext.ListingCompliances
            .Where(c => c.Status != null && c.Status == ListingComplianceStatus.Approved)
            .GroupBy(c => c.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                ReviewedAt = g.Max(x => (DateTimeOffset?)x.ReviewedAt)
            });

        return await dbContext.Listings
            .AsNoTracking()
            .Where(l => l.IsActive == true)
            .Join(
                approvedComplianceByListingId,
                l => l.Id,
                c => c.ListingId,
                (l, c) => new
                {
                    Listing = l,
                    SortAt = c.ReviewedAt ?? (DateTimeOffset?)l.CreatedAt
                })
            .OrderByDescending(x => x.SortAt)
            .Select(x => new ListingDto(
                x.Listing.Id,
                x.Listing.TenantId,
                x.Listing.SellerId,
                x.Listing.BirdDetails != null
                    ? "bird"
                    : x.Listing.LivestockDetails != null
                        ? "livestock"
                        : x.Listing.GameAnimalDetails != null
                            ? "game"
                            : x.Listing.PoultryDetails != null
                                ? "poultry"
                                : "unknown",
                x.Listing.BirdDetails != null ? x.Listing.BirdDetails.SpeciesId : null,
                x.Listing.Title,
                x.Listing.Description,
                x.Listing.StartingPrice,
                x.Listing.BuyNowPrice,
                x.Listing.CurrencyCode,
                x.Listing.Quantity,
                x.Listing.Location,
                x.Listing.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<ListingDto?> GetApprovedListingByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsActive == true)
            .Where(x => dbContext.ListingCompliances.Any(c => c.ListingId == x.Id && c.Status != null && c.Status == ListingComplianceStatus.Approved))
            .Select(x => new ListingDto(
                x.Id,
                x.TenantId,
                x.SellerId,
                x.BirdDetails != null
                    ? "bird"
                    : x.LivestockDetails != null
                        ? "livestock"
                        : x.GameAnimalDetails != null
                            ? "game"
                            : x.PoultryDetails != null
                                ? "poultry"
                                : "unknown",
                x.BirdDetails != null ? x.BirdDetails.SpeciesId : null,
                x.Title,
                x.Description,
                x.StartingPrice,
                x.BuyNowPrice,
                x.CurrencyCode,
                x.Quantity,
                x.Location,
                x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return listing;
    }
}
