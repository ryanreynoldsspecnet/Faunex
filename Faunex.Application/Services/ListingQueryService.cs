using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class ListingQueryService(IApplicationDbContext dbContext, ITenantContext tenantContext) : IListingQueryService
{
    public async Task<PagedResult<ListingDto>> BrowseAsync(ListingQuery query, CancellationToken cancellationToken = default)
    {
        // Public browsing (anonymous/buyer): only approved + active listings.
        // If auth is later introduced, buyers will remain constrained to this view.

        var (skip, take, activeOnly) = NormalizePaging(query, defaultActiveOnly: true);

        var q = ApplyFilters(dbContext.Listings.AsNoTracking(), query);

        q = q.Where(x => x.IsActive)
            .Where(x => x.Compliance != null && x.Compliance.Status == ListingComplianceStatus.Approved);

        if (activeOnly)
        {
            q = q.Where(x => x.IsActive);
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);

        return new PagedResult<ListingDto>(items, total);
    }

    public async Task<PagedResult<ListingDto>> GetMyListingsAsync(Guid sellerId, ListingQuery query, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);

        // Seller sees their own listings in any compliance state. Tenant admin also allowed.
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.TenantAdmin, FaunexRoles.Seller);
        ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, sellerId);

        var (skip, take, activeOnly) = NormalizePaging(query, defaultActiveOnly: false);

        var q = ApplyFilters(dbContext.Listings.AsNoTracking(), query)
            .Where(x => x.SellerId == sellerId);

        if (activeOnly)
        {
            q = q.Where(x => x.IsActive);
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);

        return new PagedResult<ListingDto>(items, total);
    }

    public async Task<PagedResult<ListingDto>> GetTenantListingsAsync(ListingQuery query, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.TenantAdmin);

        var (skip, take, activeOnly) = NormalizePaging(query, defaultActiveOnly: false);

        var q = ApplyFilters(dbContext.Listings.AsNoTracking(), query);

        if (activeOnly)
        {
            q = q.Where(x => x.IsActive);
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);

        return new PagedResult<ListingDto>(items, total);
    }

    public async Task<PagedResult<ListingDto>> GetAllListingsAsync(ListingQuery query, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsurePlatformAdmin(tenantContext);
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.PlatformSuperAdmin, FaunexRoles.PlatformComplianceAdmin, FaunexRoles.PlatformSupport);

        var (skip, take, activeOnly) = NormalizePaging(query, defaultActiveOnly: false);

        var q = ApplyFilters(dbContext.Listings.AsNoTracking(), query);

        if (activeOnly)
        {
            q = q.Where(x => x.IsActive);
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);

        return new PagedResult<ListingDto>(items, total);
    }

    private static IQueryable<Listing> ApplyFilters(IQueryable<Listing> q, ListingQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.AnimalClass))
        {
            q = query.AnimalClass.Trim().ToLowerInvariant() switch
            {
                "bird" => q.Where(x => x.BirdDetails != null),
                "livestock" => q.Where(x => x.LivestockDetails != null),
                "game" => q.Where(x => x.GameAnimalDetails != null),
                "poultry" => q.Where(x => x.PoultryDetails != null),
                _ => q
            };
        }

        if (query.SpeciesId.HasValue)
        {
            // Bird-only filter; if callers send SpeciesId for other animal classes it will just yield few/none.
            q = q.Where(x => x.BirdDetails != null && x.BirdDetails.SpeciesId == query.SpeciesId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            q = q.Where(x => x.Location != null && x.Location.Contains(query.Location));
        }

        return q;
    }

    private static (int skip, int take, bool activeOnly) NormalizePaging(ListingQuery query, bool defaultActiveOnly)
    {
        var take = query.Take <= 0 ? 20 : Math.Min(query.Take, 200);
        var skip = query.Skip < 0 ? 0 : query.Skip;
        var activeOnly = query.ActiveOnly ?? defaultActiveOnly;
        return (skip, take, activeOnly);
    }

    private static System.Linq.Expressions.Expression<Func<Listing, ListingDto>> MapToDtoExpression() =>
        x => new ListingDto(
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
            x.IsActive
        );
}
