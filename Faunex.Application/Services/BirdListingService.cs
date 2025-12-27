using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class BirdListingService(IApplicationDbContext dbContext, ITenantContext tenantContext) : IBirdListingService
{
    public async Task<BirdListingDto?> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);

        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.BirdDetails)
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null)
        {
            return null;
        }

        if (IsBuyer(tenantContext) && listing.Compliance?.Status != ListingComplianceStatus.Approved)
        {
            return null;
        }

        return MapToDto(listing);
    }

    public async Task<IReadOnlyList<BirdListingDto>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);

        var query = dbContext.Listings
            .AsNoTracking()
            .Include(x => x.BirdDetails)
            .Include(x => x.Compliance)
            .Where(x => x.SellerId == sellerId);

        if (IsBuyer(tenantContext))
        {
            query = query.Where(x => x.Compliance != null && x.Compliance.Status == ListingComplianceStatus.Approved);
        }

        var listings = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return listings.Select(MapToDto).ToList();
    }

    public async Task<Guid> CreateAsync(BirdListingDto listing, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "create listings");
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.TenantAdmin, FaunexRoles.Seller);
        ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, listing.SellerId);

        var listingId = listing.Id == Guid.Empty ? Guid.NewGuid() : listing.Id;

        var entity = new Listing
        {
            Id = listingId,
            TenantId = tenantContext.TenantId!.Value,
            SellerId = listing.SellerId,
            Title = listing.Title,
            Description = listing.Description,
            StartingPrice = listing.StartingPrice,
            BuyNowPrice = listing.BuyNowPrice,
            CurrencyCode = listing.CurrencyCode,
            Quantity = listing.Quantity,
            Location = listing.Location,
            IsActive = false,
            BirdDetails = new BirdDetails
            {
                ListingId = listingId,
                SpeciesId = listing.SpeciesId
            },
            Compliance = new ListingCompliance
            {
                ListingId = listingId,
                TenantId = tenantContext.TenantId.Value,
                Status = ListingComplianceStatus.Draft,
                LastUpdatedAt = DateTimeOffset.UtcNow
            }
        };

        dbContext.Listings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task<ListingDto> CreateBirdListingAsync(CreateBirdListingRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Title is required.");
        }

        if (request.Price <= 0)
        {
            throw new ArgumentException("Price must be greater than 0.");
        }

        if (request.SpeciesId == Guid.Empty)
        {
            throw new ArgumentException("SpeciesId is required.");
        }

        // Auth rules (MANDATORY)
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "create listings");
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.Seller, FaunexRoles.TenantAdmin);

        if (tenantContext is not ITenantContextWithActor actor || !actor.ActorId.HasValue)
        {
            throw new UnauthorizedAccessException("Seller identity is required.");
        }

        if (actor.ActorId.Value != request.SellerId)
        {
            throw new UnauthorizedAccessException("Seller can only create listings for themselves.");
        }

        if (!tenantContext.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException("TenantId is required.");
        }

        // TODO: Auction creation is not implemented in this slice.
        _ = request.IsAuction;

        var speciesExists = await dbContext.BirdDetails
            .IgnoreQueryFilters()
            .Select(x => x.SpeciesId)
            .Where(x => x.HasValue)
            .AnyAsync(x => x == request.SpeciesId, cancellationToken);

        if (!speciesExists)
        {
            // Fallback validation using seeded ids if species table is not exposed via IApplicationDbContext.
            // TODO: Add BirdSpecies DbSet to IApplicationDbContext to validate species properly.
            throw new ArgumentException("SpeciesId does not exist.");
        }

        var listingId = Guid.NewGuid();
        var tenantId = tenantContext.TenantId.Value;

        var entity = new Listing
        {
            Id = listingId,
            TenantId = tenantId,
            SellerId = request.SellerId,
            Title = request.Title,
            Description = request.Description,
            StartingPrice = request.Price,
            BuyNowPrice = request.IsAuction ? null : request.Price,
            CurrencyCode = "USD",
            Quantity = 1,
            Location = null,
            IsActive = false,
            BirdDetails = new BirdDetails
            {
                ListingId = listingId,
                SpeciesId = request.SpeciesId
            },
            Compliance = new ListingCompliance
            {
                ListingId = listingId,
                TenantId = tenantId,
                Status = ListingComplianceStatus.Draft,
                LastUpdatedAt = DateTimeOffset.UtcNow
            }
        };

        dbContext.Listings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ListingDto(
            entity.Id,
            entity.TenantId,
            entity.SellerId,
            AnimalClass: "Bird",
            SpeciesId: entity.BirdDetails?.SpeciesId,
            entity.Title,
            entity.Description,
            entity.StartingPrice,
            entity.BuyNowPrice,
            entity.CurrencyCode,
            entity.Quantity,
            entity.Location,
            entity.IsActive);
    }

    public async Task UpdateAsync(BirdListingDto listing, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "update listings");
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.TenantAdmin, FaunexRoles.Seller);

        var entity = await dbContext.Listings
            .Include(x => x.BirdDetails)
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listing.Id, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, entity.SellerId);

        entity.SellerId = listing.SellerId;
        entity.Title = listing.Title;
        entity.Description = listing.Description;
        entity.StartingPrice = listing.StartingPrice;
        entity.BuyNowPrice = listing.BuyNowPrice;
        entity.CurrencyCode = listing.CurrencyCode;
        entity.Quantity = listing.Quantity;
        entity.Location = listing.Location;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (entity.BirdDetails is null)
        {
            entity.BirdDetails = new BirdDetails
            {
                ListingId = entity.Id,
                SpeciesId = listing.SpeciesId
            };
        }
        else
        {
            entity.BirdDetails.SpeciesId = listing.SpeciesId;
        }

        // Any substantive change should require re-review if previously approved.
        // TODO: Decide which fields trigger re-review across verticals.
        if (entity.Compliance is not null && entity.Compliance.Status == ListingComplianceStatus.Approved)
        {
            entity.Compliance.Status = ListingComplianceStatus.Draft;
            entity.Compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
            entity.IsActive = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "deactivate listings");
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.TenantAdmin, FaunexRoles.Seller);

        var entity = await dbContext.Listings
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (entity is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, entity.SellerId);

        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        if (entity.Compliance is not null && entity.Compliance.Status == ListingComplianceStatus.Approved)
        {
            entity.Compliance.Status = ListingComplianceStatus.Suspended;
            entity.Compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
            entity.Compliance.ReviewNotes = "Deactivated by tenant.";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SubmitForReviewAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "submit listings for compliance review");
        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.TenantAdmin, FaunexRoles.Seller);

        var listing = await dbContext.Listings
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, listing.SellerId);

        var compliance = listing.Compliance;
        if (compliance is null)
        {
            compliance = new ListingCompliance
            {
                ListingId = listing.Id,
                TenantId = listing.TenantId,
                Status = ListingComplianceStatus.Draft,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
            listing.Compliance = compliance;
        }

        var required = GetRequiredDocumentTypesForListing(listing);

        var uploadedTypes = await dbContext.ListingDocuments
            .AsNoTracking()
            .Where(x => x.ListingId == listing.Id && x.UploadedAt != null)
            .Select(x => x.DocumentType)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missing = required.Except(uploadedTypes).ToList();

        if (missing.Count > 0)
        {
            compliance.Status = ListingComplianceStatus.PendingDocuments;
            compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
            listing.IsActive = false;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        compliance.Status = ListingComplianceStatus.UnderReview;
        compliance.SubmittedAt = DateTimeOffset.UtcNow;
        compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
        listing.IsActive = false;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task ApproveListingAsync(Guid listingId, string? notes, CancellationToken cancellationToken = default) =>
        ReviewAsync(listingId, ListingComplianceStatus.Approved, notes, cancellationToken);

    public Task RejectListingAsync(Guid listingId, string? notes, CancellationToken cancellationToken = default) =>
        ReviewAsync(listingId, ListingComplianceStatus.Rejected, notes, cancellationToken);

    public Task SuspendListingAsync(Guid listingId, string? notes, CancellationToken cancellationToken = default) =>
        ReviewAsync(listingId, ListingComplianceStatus.Suspended, notes, cancellationToken);

    private async Task ReviewAsync(Guid listingId, ListingComplianceStatus newStatus, string? notes, CancellationToken cancellationToken)
    {
        ServiceAuthorization.EnsurePlatformAdmin(tenantContext);
        ServiceAuthorization.EnsurePlatformComplianceAdmin(tenantContext);

        var listing = await dbContext.Listings
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        if (listing.Compliance is null)
        {
            listing.Compliance = new ListingCompliance
            {
                ListingId = listing.Id,
                TenantId = listing.TenantId,
                Status = ListingComplianceStatus.Draft,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
        }

        listing.Compliance.Status = newStatus;
        listing.Compliance.ReviewNotes = notes;
        listing.Compliance.ReviewedAt = DateTimeOffset.UtcNow;
        listing.Compliance.LastUpdatedAt = DateTimeOffset.UtcNow;

        if (tenantContext is ITenantContextWithActor actor)
        {
            listing.Compliance.ReviewedByUserId = actor.ActorId;
        }

        listing.IsActive = newStatus == ListingComplianceStatus.Approved;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyCollection<ListingDocumentType> GetRequiredDocumentTypesForListing(Listing listing)
    {
        if (listing.BirdDetails is not null)
        {
            // TODO: refine per species/CITES appendix.
            return new[] { ListingDocumentType.CitesPermit, ListingDocumentType.VeterinaryCertificate };
        }

        if (listing.LivestockDetails is not null)
        {
            // TODO: refine by livestock class.
            return new[] { ListingDocumentType.HealthCertificate, ListingDocumentType.TransferOfOwnership };
        }

        if (listing.GameAnimalDetails is not null)
        {
            // TODO: refine by region.
            return new[] { ListingDocumentType.GamePermit };
        }

        if (listing.PoultryDetails is not null)
        {
            // TODO: refine by poultry type.
            return new[] { ListingDocumentType.PoultryHealthCertificate, ListingDocumentType.TransportPermit };
        }

        return Array.Empty<ListingDocumentType>();
    }

    private static bool IsBuyer(ITenantContext tenantContext)
    {
        if (tenantContext is ITenantContextWithRoles withRoles)
        {
            return withRoles.Roles.Contains(FaunexRoles.Buyer);
        }

        return false;
    }

    private static BirdListingDto MapToDto(Listing listing)
    {
        var speciesId = listing.BirdDetails?.SpeciesId ?? Guid.Empty;

        return new BirdListingDto(
            listing.Id,
            listing.SellerId,
            speciesId,
            listing.Title,
            listing.Description,
            listing.StartingPrice,
            listing.BuyNowPrice,
            listing.CurrencyCode,
            listing.Quantity,
            listing.Location,
            listing.IsActive
        );
    }

    public async Task<IReadOnlyList<ListingDto>> GetSellerListingsAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        if (tenantContext.IsPlatformAdmin)
        {
            throw new UnauthorizedAccessException("Platform admin cannot access seller listings view.");
        }

        if (!tenantContext.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException("TenantId is required.");
        }

        if (tenantContext is not ITenantContextWithActor actor || !actor.ActorId.HasValue)
        {
            throw new UnauthorizedAccessException("Authenticated user context is required.");
        }

        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.Seller, FaunexRoles.TenantAdmin);

        if (sellerId != actor.ActorId.Value)
        {
            throw new UnauthorizedAccessException("Cannot query listings for another seller.");
        }

        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.SellerId == sellerId)
            .OrderByDescending(x => x.CreatedAt)
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

    public async Task SubmitForComplianceAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        if (tenantContext.IsPlatformAdmin)
        {
            throw new UnauthorizedAccessException("Platform admin cannot submit listings for compliance.");
        }

        if (!tenantContext.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException("TenantId is required.");
        }

        if (tenantContext is not ITenantContextWithActor actor || !actor.ActorId.HasValue)
        {
            throw new UnauthorizedAccessException("Authenticated user context is required.");
        }

        ServiceAuthorization.EnsureRole(tenantContext, FaunexRoles.Seller, FaunexRoles.TenantAdmin);

        var listing = await dbContext.Listings
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        if (listing.TenantId != tenantContext.TenantId.Value)
        {
            // With global filters this should not usually happen, but keep it explicit.
            throw new UnauthorizedAccessException("Listing does not belong to the current tenant.");
        }

        var isTenantAdmin = tenantContext is ITenantContextWithRoles withRoles && withRoles.Roles.Contains(FaunexRoles.TenantAdmin);

        if (!isTenantAdmin)
        {
            // Seller anti-spoofing: seller can only submit their own listing.
            if (listing.SellerId != actor.ActorId.Value)
            {
                throw new UnauthorizedAccessException("Seller can only submit their own listings for compliance.");
            }
        }

        listing.IsActive = false;

        if (listing.Compliance is null)
        {
            listing.Compliance = new ListingCompliance
            {
                ListingId = listing.Id,
                TenantId = listing.TenantId,
                Status = ListingComplianceStatus.Draft,
                LastUpdatedAt = DateTimeOffset.UtcNow
            };
        }

        if (listing.Compliance.Status != ListingComplianceStatus.Draft)
        {
            throw new InvalidOperationException("Listing compliance must be in Draft status to submit.");
        }

        var required = GetRequiredDocumentTypesForListing(listing);

        var uploadedTypes = await dbContext.ListingDocuments
            .AsNoTracking()
            .Where(x => x.ListingId == listing.Id && x.UploadedAt != null)
            .Select(x => x.DocumentType)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missing = required.Except(uploadedTypes).ToList();

        if (missing.Count > 0)
        {
            listing.Compliance.Status = ListingComplianceStatus.PendingDocuments;
            listing.Compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            listing.Compliance.Status = ListingComplianceStatus.UnderReview;
            listing.Compliance.SubmittedAt = DateTimeOffset.UtcNow;
            listing.Compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
