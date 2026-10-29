using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Compliance;

public sealed class ListingReviewQueries(ApplicationDbContext db, ITenantContext tenant)
{
    public async Task<ReviewQueueResult> QueueAsync(string? search, string? status, int skip, int take, CancellationToken ct)
    {
        ServiceAuthorization.EnsurePlatformAdmin(tenant);
        ServiceAuthorization.EnsurePlatformComplianceAdmin(tenant);
        if (skip < 0 || take is < 1 or > 100 || search?.Length > 160) throw new ListingValidationException("Invalid queue filters.");
        var rows = from listing in db.Listings.AsNoTracking()
                   join company in db.Tenants on listing.TenantId equals company.Id
                   join seller in db.Set<User>() on listing.SellerId equals seller.Id
                   where listing.Compliance != null
                   select new { Listing = listing, TenantName = company.Name, SellerName = seller.DisplayName };
        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<ListingComplianceStatus>(status ?? "UnderReview", true, out var state) || !Enum.IsDefined(state))
                throw new ListingValidationException("Select a valid review status.");
            rows = rows.Where(x => x.Listing.Compliance!.Status == state);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Listing.Title.ToLower().Contains(term) || x.TenantName.ToLower().Contains(term) || x.SellerName.ToLower().Contains(term));
        }
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Listing.Compliance!.SubmittedAt ?? x.Listing.CreatedAt).ThenBy(x => x.Listing.Id)
            .Skip(skip).Take(take).Select(x => new ReviewQueueRow(x.Listing.Id, x.TenantName, x.SellerName, x.Listing.Title,
                x.Listing.Compliance!.Status.ToString(), x.Listing.Compliance.SubmittedAt,
                db.ListingFiles.Count(f => f.ListingId == x.Listing.Id && f.TenantId == x.Listing.TenantId && f.DeletedAt == null))).ToListAsync(ct);
        return new(items, total, skip, take);
    }

    public async Task<ListingReviewDto> DetailAsync(Guid id, CancellationToken ct)
    {
        var reviewer = tenant.IsPlatformAdmin;
        if (reviewer) { ServiceAuthorization.EnsurePlatformAdmin(tenant); ServiceAuthorization.EnsurePlatformComplianceAdmin(tenant); }
        else ServiceAuthorization.EnsureTenantAdminOrSeller(tenant);
        var listing = await db.Listings.AsNoTracking().Include(x => x.Compliance).Include(x => x.BirdDetails).ThenInclude(x => x!.Species)
            .Include(x => x.LivestockDetails).Include(x => x.GameAnimalDetails).Include(x => x.PoultryDetails)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Listing not found.");
        if (!reviewer) ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenant, listing.SellerId);
        var company = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == listing.TenantId, ct);
        var seller = await db.Set<User>().AsNoTracking().SingleAsync(x => x.Id == listing.SellerId, ct);
        var history = await db.ListingReviewEvents.AsNoTracking().Where(x => x.ListingId == id && x.TenantId == listing.TenantId)
            .OrderByDescending(x => x.Id).Take(200).ToListAsync(ct);
        var animalClass = listing.BirdDetails is not null ? "bird" : listing.LivestockDetails is not null ? "livestock" : listing.GameAnimalDetails is not null ? "game" : listing.PoultryDetails is not null ? "poultry" : "unknown";
        var required = await db.ListingDocumentRequirements.AsNoTracking().Where(x => x.TenantId == listing.TenantId && x.AnimalClass == animalClass).Select(x => x.DocumentType).ToListAsync(ct);
        var auctions = await db.Auctions.AnyAsync(x => x.ListingId == id && x.TenantId == listing.TenantId, ct);
        var state = listing.Compliance?.Status ?? ListingComplianceStatus.Draft;
        var files = await db.ListingFiles.CountAsync(x => x.ListingId == id && x.TenantId == listing.TenantId && x.DeletedAt == null, ct);
        return new(new(id, listing.TenantId, listing.SellerId, animalClass, listing.BirdDetails?.SpeciesId, listing.Title, listing.Description,
            listing.StartingPrice, listing.BuyNowPrice, listing.CurrencyCode, listing.Quantity, listing.Location, listing.IsActive),
            company.Name, seller.DisplayName, listing.Revision, state.ToString(), listing.Compliance?.ReviewNotes,
            !reviewer && !auctions && state is ListingComplianceStatus.Draft or ListingComplianceStatus.Rejected or ListingComplianceStatus.PendingDocuments,
            reviewer && company.IsActive && !auctions, auctions, files, listing.BirdDetails?.Species?.CommonName,
            listing.BirdDetails?.RingNumber, listing.BirdDetails?.MicrochipNumber, listing.BirdDetails?.Sex, listing.BirdDetails?.HatchDate,
            required.Select(x => x.ToString()).ToList(), history.Select(x => new ReviewEventDto(x.Id, x.FromStatus?.ToString(), x.ToStatus.ToString(), x.Revision, x.ActorId, x.ActorKind, x.Notes, x.OccurredAt)).ToList());
    }
}
