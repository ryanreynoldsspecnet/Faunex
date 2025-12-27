using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class ListingBrowseService(IApplicationDbContext dbContext) : IListingBrowseService
{
    public async Task<IReadOnlyList<ListingDto>> BrowseApprovedListingsAsync(CancellationToken cancellationToken)
    {
        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.IsActive && x.Compliance != null && x.Compliance.Status == ListingComplianceStatus.Approved)
            .OrderByDescending(x => x.Compliance!.ReviewedAt ?? x.CreatedAt)
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
}
