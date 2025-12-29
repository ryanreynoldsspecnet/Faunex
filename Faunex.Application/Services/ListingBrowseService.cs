using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class ListingBrowseService(IApplicationDbContext dbContext) : IListingBrowseService
{
    public async Task<IReadOnlyList<ListingDto>> BrowseApprovedListingsAsync(CancellationToken cancellationToken)
    {
        // IMPORTANT:
        // Tenant scoping is enforced by global query filters in ApplicationDbContext.
        // The ListingCompliance entity is tenant-scoped separately, and when those filters exclude rows
        // the Listing.Compliance navigation can be null at query time. Avoid depending on the navigation
        // for filtering/ordering to prevent NullReferenceExceptions translating to 500.

        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Where(x => dbContext.ListingCompliances.Any(c => c.ListingId == x.Id && c.Status == ListingComplianceStatus.Approved))
            .OrderByDescending(x => dbContext.ListingCompliances
                .Where(c => c.ListingId == x.Id)
                .Select(c => c.ReviewedAt)
                .FirstOrDefault() ?? x.CreatedAt)
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
            .ToListAsync(cancellationToken);

        return listings;
    }

    public async Task<ListingDto?> GetApprovedListingByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Where(x => dbContext.ListingCompliances.Any(c => c.ListingId == x.Id && c.Status == ListingComplianceStatus.Approved))
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
