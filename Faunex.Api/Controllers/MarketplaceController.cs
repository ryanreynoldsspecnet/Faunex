using Faunex.Api.Tenancy;
using Faunex.Application.DTOs;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/marketplace")]
public sealed class MarketplaceController(ApplicationDbContext db, TenantDomainResolver tenantDomainResolver, TimeProvider clock) : ControllerBase
{
    [HttpGet("context")]
    public async Task<ActionResult<MarketplaceContextDto>> GetContext([FromQuery] string? host, CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(host, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        return Ok(ToContextDto(tenant));
    }

    [HttpGet("listings")]
    public async Task<ActionResult<IReadOnlyList<ListingDto>>> BrowseListings([FromQuery] string? host, CancellationToken cancellationToken)
    {
        var tenantId = await tenantDomainResolver.ResolveTenantIdAsync(host, cancellationToken);
        if (tenantId is null)
        {
            return NotFound();
        }

        var listings = await QueryApprovedListings(tenantId.Value)
            .OrderByDescending(x => x.SortAt)
            .Select(x => x.Listing)
            .ToListAsync(cancellationToken);

        return Ok(await ToListingDtosAsync(listings, cancellationToken));
    }

    [HttpGet("listings/{listingId:guid}")]
    public async Task<ActionResult<ListingDto>> GetListing(Guid listingId, [FromQuery] string? host, CancellationToken cancellationToken)
    {
        var tenantId = await tenantDomainResolver.ResolveTenantIdAsync(host, cancellationToken);
        if (tenantId is null)
        {
            return NotFound();
        }

        var listing = await QueryApprovedListings(tenantId.Value)
            .Where(x => x.Listing.Id == listingId)
            .Select(x => x.Listing)
            .FirstOrDefaultAsync(cancellationToken);

        if (listing is null)
        {
            return NotFound();
        }

        var dto = (await ToListingDtosAsync([listing], cancellationToken)).Single();
        return Ok(dto);
    }

    [HttpGet("listings/{listingId:guid}/auctions")]
    public async Task<ActionResult<IReadOnlyList<PublicAuctionDto>>> GetListingAuctions(Guid listingId, [FromQuery] string? host, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var tenantId = await tenantDomainResolver.ResolveTenantIdAsync(host, cancellationToken);
        if (tenantId is null) return NotFound();
        if (!await db.Listings.IgnoreQueryFilters().AnyAsync(x => x.Id == listingId && x.TenantId == tenantId && x.IsActive
            && x.Compliance != null && x.Compliance.TenantId == tenantId && x.Compliance.Status == ListingComplianceStatus.Approved, cancellationToken)) return NotFound();
        var auctions = await PublicAuctionQuery(tenantId.Value).Where(x => x.ListingId == listingId).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        return Ok(auctions.Select(x => Faunex.Application.Services.TimedAuctionRules.Public(x, now)).ToList());
    }

    [HttpGet("auctions/{auctionId:guid}/price")]
    public async Task<ActionResult<CurrentPriceDto>> GetAuctionPrice(Guid auctionId, [FromQuery] string? host, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var tenantId = await tenantDomainResolver.ResolveTenantIdAsync(host, cancellationToken);
        if (tenantId is null) return NotFound();
        var auction = await PublicAuctionQuery(tenantId.Value).SingleOrDefaultAsync(x => x.Id == auctionId, cancellationToken);
        return auction is null ? NotFound() : Ok(new CurrentPriceDto(auction.LeadingAmount));
    }

    [HttpGet("auctions/{auctionId:guid}/bids")]
    public async Task<ActionResult<PagedResult<PublicBidDto>>> GetAuctionBids(Guid auctionId, [FromQuery] string? host,
        [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        var tenantId = await tenantDomainResolver.ResolveTenantIdAsync(host, cancellationToken);
        if (tenantId is null || !await PublicAuctionQuery(tenantId.Value).AnyAsync(x => x.Id == auctionId, cancellationToken)) return NotFound();
        var query = db.Bids.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == tenantId && x.AuctionId == auctionId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.BidNumber).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 100))
            .Select(x => new PublicBidDto(x.Amount, x.CurrencyCode, x.PlacedAt)).ToListAsync(cancellationToken);
        return Ok(new PagedResult<PublicBidDto>(items, total));
    }

    private async Task<Faunex.Domain.Entities.Tenant?> ResolveTenantAsync(string? host, CancellationToken cancellationToken)
    {
        var tenantId = await tenantDomainResolver.ResolveTenantIdAsync(host, cancellationToken);
        if (tenantId is null)
        {
            return null;
        }

        return await db.Tenants
            .AsNoTracking()
            .Include(x => x.Domains)
            .FirstOrDefaultAsync(x => x.Id == tenantId.Value && x.IsActive, cancellationToken);
    }

    private IQueryable<ApprovedListingRow> QueryApprovedListings(Guid tenantId)
    {
        var approvedComplianceByListingId = db.ListingCompliances
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.Status == ListingComplianceStatus.Approved)
            .GroupBy(c => c.ListingId)
            .Select(g => new
            {
                ListingId = g.Key,
                ReviewedAt = g.Max(x => (DateTimeOffset?)x.ReviewedAt)
            });

        return db.Listings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.IsActive)
            .Join(
                approvedComplianceByListingId,
                l => l.Id,
                c => c.ListingId,
                (listing, compliance) => new ApprovedListingRow
                {
                    Listing = listing,
                    SortAt = compliance.ReviewedAt ?? (DateTimeOffset?)listing.CreatedAt
                });
    }

    private IQueryable<Auction> PublicAuctionQuery(Guid tenantId) => db.Auctions.IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.TenantId == tenantId && x.RulesVersion == 1 && x.Type == AuctionType.Timed && !x.IsSealedBid && x.State != AuctionState.Draft
            && x.Listing != null && x.Listing.TenantId == tenantId && x.CurrencyCode == x.Listing.CurrencyCode && x.Listing.IsActive && x.Listing.Compliance != null
            && x.Listing.Compliance.TenantId == tenantId && x.Listing.Compliance.Status == ListingComplianceStatus.Approved);

    private async Task<IReadOnlyList<ListingDto>> ToListingDtosAsync(IReadOnlyList<Listing> listings, CancellationToken cancellationToken)
    {
        if (listings.Count == 0)
        {
            return [];
        }

        var listingIds = listings.Select(x => x.Id).ToArray();

        var birdDetails = await db.BirdDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId))
            .Select(x => new { x.ListingId, x.SpeciesId })
            .ToDictionaryAsync(x => x.ListingId, x => (Guid?)x.SpeciesId, cancellationToken);

        var livestockIds = await db.LivestockDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId))
            .Select(x => x.ListingId)
            .ToListAsync(cancellationToken);

        var gameIds = await db.GameAnimalDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId))
            .Select(x => x.ListingId)
            .ToListAsync(cancellationToken);

        var poultryIds = await db.PoultryDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.ListingId))
            .Select(x => x.ListingId)
            .ToListAsync(cancellationToken);

        var livestockSet = livestockIds.ToHashSet();
        var gameSet = gameIds.ToHashSet();
        var poultrySet = poultryIds.ToHashSet();

        return listings
            .Select(x =>
            {
                var animalClass = birdDetails.ContainsKey(x.Id)
                    ? "bird"
                    : livestockSet.Contains(x.Id)
                        ? "livestock"
                        : gameSet.Contains(x.Id)
                            ? "game"
                            : poultrySet.Contains(x.Id)
                                ? "poultry"
                                : "unknown";

                birdDetails.TryGetValue(x.Id, out var speciesId);

                return new ListingDto(
                    x.Id,
                    x.TenantId,
                    x.SellerId,
                    animalClass,
                    speciesId,
                    x.Title,
                    x.Description,
                    x.StartingPrice,
                    x.BuyNowPrice,
                    x.CurrencyCode,
                    x.Quantity,
                    x.Location,
                    x.IsActive);
            })
            .ToList();
    }

    private static MarketplaceContextDto ToContextDto(Faunex.Domain.Entities.Tenant tenant)
    {
        var domains = tenant.Domains
            .Where(x => x.IsActive && x.VerifiedAt != null)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Hostname)
            .Select(x => x.Hostname)
            .ToArray();

        var primaryDomain = tenant.Domains
            .Where(x => x.IsActive && x.VerifiedAt != null)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Hostname)
            .Select(x => x.Hostname)
            .FirstOrDefault();

        return new MarketplaceContextDto(
            tenant.Id,
            tenant.Name,
            tenant.CompanyName,
            tenant.MarketplaceDisplayName,
            tenant.MarketplaceTagline,
            tenant.LogoUrl,
            tenant.BrandPrimaryColor,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.SupportEmail,
            tenant.SupportPhone,
            primaryDomain,
            domains);
    }

    private sealed class ApprovedListingRow
    {
        public Listing Listing { get; init; } = null!;
        public DateTimeOffset? SortAt { get; init; }
    }
}

public sealed record MarketplaceContextDto(
    Guid TenantId,
    string Name,
    string? CompanyName,
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? ContactEmail,
    string? ContactPhone,
    string? SupportEmail,
    string? SupportPhone,
    string? PrimaryDomain,
    IReadOnlyList<string> Domains);
