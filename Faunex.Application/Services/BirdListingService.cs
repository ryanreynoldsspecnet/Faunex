using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Faunex.Application.Services;

public sealed class BirdListingService(IApplicationDbContext dbContext, ITenantContext tenantContext, IListingFileStorage fileStorage) : IBirdListingService
{
    public async Task<BirdListingDto?> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);

        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.BirdDetails)
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null || listing.BirdDetails is null)
        {
            return null;
        }

        if (IsBuyer(tenantContext) && (listing.Compliance?.Status != ListingComplianceStatus.Approved || !listing.IsActive))
        {
            return null;
        }

        if (!tenantContext.IsPlatformAdmin && !IsBuyer(tenantContext))
            ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, listing.SellerId);
        return MapToDto(listing, await dbContext.Auctions.AnyAsync(x => x.ListingId == listingId, cancellationToken));
    }

    public async Task<IReadOnlyList<BirdListingDto>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureTenantUser(tenantContext);

        if (!tenantContext.IsPlatformAdmin && !IsBuyer(tenantContext))
            ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, sellerId);
        var query = dbContext.Listings
            .AsNoTracking()
            .Include(x => x.BirdDetails)
            .Include(x => x.Compliance)
            .Where(x => x.SellerId == sellerId && x.BirdDetails != null);

        if (IsBuyer(tenantContext))
        {
            query = query.Where(x => x.IsActive && x.Compliance != null && x.Compliance.Status == ListingComplianceStatus.Approved);
        }

        var listings = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return listings.Select(x => MapToDto(x)).ToList();
    }

    public async Task<Guid> CreateAsync(BirdListingDto listing, CancellationToken cancellationToken = default) =>
        (await CreateEntityAsync(listing, cancellationToken)).Id;

    public async Task<ListingDto> CreateBirdListingAsync(CreateBirdListingRequest request, CancellationToken cancellationToken)
    {
        var entity = await CreateEntityAsync(new BirdListingDto(
            Guid.Empty, request.SellerId, request.SpeciesId, request.Title, request.Description,
            request.Price, request.IsAuction ? null : request.Price, request.CurrencyCode,
            request.Quantity, request.Location, false, request.RingNumber, request.MicrochipNumber,
            request.Sex, request.HatchDate), cancellationToken);
        return new ListingDto(entity.Id, entity.TenantId, entity.SellerId, "bird",
            entity.BirdDetails!.SpeciesId, entity.Title, entity.Description, entity.StartingPrice,
            entity.BuyNowPrice, entity.CurrencyCode, entity.Quantity, entity.Location, entity.IsActive);
    }

    private async Task<Listing> CreateEntityAsync(BirdListingDto input, CancellationToken cancellationToken)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "create listings");
        ServiceAuthorization.EnsureTenantAdminOrSeller(tenantContext);
        if (tenantContext is not ITenantContextWithActor actor || actor.ActorId != input.SellerId)
            throw new UnauthorizedAccessException("Create listings under your own account.");
        await ValidateAsync(input, cancellationToken);
        var entity = new Listing
        {
            TenantId = tenantContext.TenantId!.Value,
            SellerId = input.SellerId,
            Title = input.Title.Trim(),
            Description = Clean(input.Description),
            StartingPrice = input.StartingPrice,
            BuyNowPrice = input.BuyNowPrice,
            CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant(),
            Quantity = input.Quantity,
            Location = Clean(input.Location),
            IsActive = false,
            BirdDetails = new BirdDetails
            {
                SpeciesId = input.SpeciesId,
                RingNumber = Clean(input.RingNumber),
                MicrochipNumber = Clean(input.MicrochipNumber),
                Sex = Clean(input.Sex),
                HatchDate = input.HatchDate
            },
            Compliance = new ListingCompliance
            {
                TenantId = tenantContext.TenantId.Value,
                Status = ListingComplianceStatus.Draft
            }
        };
        dbContext.Listings.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(BirdListingDto input, CancellationToken cancellationToken = default)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenantContext, "update listings");
        ServiceAuthorization.EnsureTenantAdminOrSeller(tenantContext);
        var entity = await dbContext.Listings.Include(x => x.BirdDetails).Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == input.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Listing not found.");
        ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenantContext, entity.SellerId);
        if (input.SellerId != entity.SellerId)
            throw new UnauthorizedAccessException("Listing ownership cannot be changed.");
        if (entity.BirdDetails is null)
            throw new ListingValidationException("This editor only supports bird listings.");
        if (input.Revision is null || input.Revision != entity.Revision)
            throw new ListingEditConflictException("This listing changed. Reload it before saving.");
        if (entity.Compliance?.Status is ListingComplianceStatus.UnderReview or ListingComplianceStatus.Suspended)
            throw new ListingEditConflictException("This listing cannot be edited in its current review state.");
        if (await dbContext.Auctions.AnyAsync(x => x.ListingId == entity.Id, cancellationToken))
            throw new ListingEditConflictException("Listings with auction activity cannot be edited.");
        await ValidateAsync(input, cancellationToken);
        entity.Title = input.Title.Trim();
        entity.Description = Clean(input.Description);
        entity.StartingPrice = input.StartingPrice;
        entity.BuyNowPrice = input.BuyNowPrice;
        entity.CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant();
        entity.Quantity = input.Quantity;
        entity.Location = Clean(input.Location);
        entity.BirdDetails.SpeciesId = input.SpeciesId;
        entity.BirdDetails.RingNumber = Clean(input.RingNumber);
        entity.BirdDetails.MicrochipNumber = Clean(input.MicrochipNumber);
        entity.BirdDetails.Sex = Clean(input.Sex);
        entity.BirdDetails.HatchDate = input.HatchDate;
        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.Compliance ??= new ListingCompliance { ListingId = entity.Id, TenantId = entity.TenantId };
        entity.Compliance.Status = ListingComplianceStatus.Draft;
        entity.Compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateAsync(BirdListingDto input, CancellationToken cancellationToken)
    {
        if (input.Title?.Trim().Length is not (>= 3 and <= 160))
            throw new ListingValidationException("Title must contain 3 to 160 characters.");
        if (input.Description?.Length > 2000)
            throw new ListingValidationException("Description must be at most 2000 characters.");
        if (!ValidPrice(input.StartingPrice) || (input.BuyNowPrice is { } buyNow && (!ValidPrice(buyNow) || buyNow < input.StartingPrice)))
            throw new ListingValidationException("Prices must be between 0.01 and 999999999, with at most two decimal places. Buy-now cannot be below starting price.");
        if (input.CurrencyCode?.Trim().ToUpperInvariant() is not ("ZAR" or "USD"))
            throw new ListingValidationException("Use ZAR or the existing USD currency.");
        if (input.Quantity is < 1 or > 10000)
            throw new ListingValidationException("Quantity must be between 1 and 10000.");
        if (input.Location?.Length > 200 || input.RingNumber?.Length > 80 || input.MicrochipNumber?.Length > 80)
            throw new ListingValidationException("Location must be at most 200 characters; identifiers at most 80.");
        if (Clean(input.Sex) is not (null or "Unknown" or "Male" or "Female"))
            throw new ListingValidationException("Select Unknown, Male or Female.");
        if (input.HatchDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ListingValidationException("Hatch date cannot be in the future.");
        if (input.SpeciesId == Guid.Empty || !await dbContext.BirdSpeciesSet.AnyAsync(x => x.Id == input.SpeciesId, cancellationToken))
            throw new ListingValidationException("Select a valid bird species.");
    }

    private static bool ValidPrice(decimal price) => price is >= 0.01m and <= 999999999m && decimal.Round(price, 2) == price;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    public Task SubmitForReviewAsync(Guid listingId, CancellationToken cancellationToken = default) =>
        SubmitForComplianceAsync(listingId, cancellationToken);

    public async Task ApproveListingAsync(Guid listingId, string? notes, CancellationToken cancellationToken = default)
    {
        EnsurePlatformComplianceReviewer();

        var listing = await dbContext.Listings
            .Include(x => x.BirdDetails)
            .Include(x => x.LivestockDetails)
            .Include(x => x.GameAnimalDetails)
            .Include(x => x.PoultryDetails)
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        var compliance = listing.Compliance ?? throw new InvalidOperationException("Listing compliance is required.");

        if (compliance.Status != ListingComplianceStatus.UnderReview)
        {
            throw new InvalidOperationException("Listing compliance must be UnderReview to approve.");
        }

        var required = await GetRequiredDocumentTypesAsync(listing, cancellationToken);
        if (required.Count > 0 && required.Except(await ManagedEvidenceTypesAsync(listing, cancellationToken)).Any())
            throw new ListingValidationException("Required supporting files are missing or unavailable. Approval is blocked.");

        compliance.Status = ListingComplianceStatus.Approved;
        compliance.ReviewNotes = notes;
        compliance.ReviewedAt = DateTimeOffset.UtcNow;
        compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
        compliance.ReviewedByUserId = ((ITenantContextWithActor)tenantContext).ActorId;

        listing.IsActive = true;
        listing.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectListingAsync(Guid listingId, string? notes, CancellationToken cancellationToken = default)
    {
        EnsurePlatformComplianceReviewer();

        var listing = await dbContext.Listings
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        var compliance = listing.Compliance ?? throw new InvalidOperationException("Listing compliance is required.");

        if (compliance.Status != ListingComplianceStatus.UnderReview && compliance.Status != ListingComplianceStatus.PendingDocuments)
        {
            throw new InvalidOperationException("Listing compliance must be UnderReview or PendingDocuments to reject.");
        }

        compliance.Status = ListingComplianceStatus.Rejected;
        compliance.ReviewNotes = notes;
        compliance.ReviewedAt = DateTimeOffset.UtcNow;
        compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
        compliance.ReviewedByUserId = ((ITenantContextWithActor)tenantContext).ActorId;

        listing.IsActive = false;
        listing.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SuspendListingAsync(Guid listingId, string? notes, CancellationToken cancellationToken = default) =>
        ReviewAsync(listingId, ListingComplianceStatus.Suspended, notes, cancellationToken);

    private void EnsurePlatformComplianceReviewer()
    {
        if (tenantContext is not ITenantContextWithActor actor || !actor.ActorId.HasValue)
        {
            throw new UnauthorizedAccessException("Authenticated user context (ActorId) is required.");
        }

        ServiceAuthorization.EnsurePlatformAdmin(tenantContext);

        if (tenantContext.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException("Platform admins must not have TenantId.");
        }

        ServiceAuthorization.EnsurePlatformComplianceAdmin(tenantContext);
    }

    private async Task<IReadOnlyCollection<ListingDocumentType>> GetRequiredDocumentTypesAsync(Listing listing, CancellationToken cancellationToken)
    {
        var animalClass = listing.BirdDetails is not null ? "bird"
            : listing.LivestockDetails is not null ? "livestock"
            : listing.GameAnimalDetails is not null ? "game"
            : listing.PoultryDetails is not null ? "poultry"
            : throw new ListingValidationException("Animal classification is required before submission.");
        // No configured requirements means optional evidence, never automatic publication.
        return await dbContext.ListingDocumentRequirements.AsNoTracking()
            .Where(x => x.TenantId == listing.TenantId && x.AnimalClass == animalClass)
            .Select(x => x.DocumentType).ToListAsync(cancellationToken);
    }

    private static bool IsBuyer(ITenantContext tenantContext)
    {
        if (tenantContext is ITenantContextWithRoles withRoles)
        {
            return withRoles.Roles.Contains(FaunexRoles.Buyer)
                && !withRoles.Roles.Contains(FaunexRoles.Seller) && !withRoles.Roles.Contains(FaunexRoles.TenantAdmin);
        }

        return false;
    }

    private static BirdListingDto MapToDto(Listing listing, bool hasAuctions = false)
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
            listing.IsActive,
            listing.BirdDetails?.RingNumber,
            listing.BirdDetails?.MicrochipNumber,
            listing.BirdDetails?.Sex,
            listing.BirdDetails?.HatchDate,
            listing.Revision,
            listing.Compliance?.Status.ToString() ?? "Draft",
            hasAuctions
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
            .Include(x => x.BirdDetails)
            .Include(x => x.LivestockDetails)
            .Include(x => x.GameAnimalDetails)
            .Include(x => x.PoultryDetails)
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
        listing.UpdatedAt = DateTimeOffset.UtcNow;

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

        if (listing.Compliance.Status is not (ListingComplianceStatus.Draft or ListingComplianceStatus.PendingDocuments or ListingComplianceStatus.Rejected))
        {
            throw new ListingEditConflictException("Only draft, pending-document or rejected listings can be submitted.");
        }

        var required = await GetRequiredDocumentTypesAsync(listing, cancellationToken);

        IReadOnlyCollection<ListingDocumentType> uploadedTypes = required.Count == 0 ? [] : await ManagedEvidenceTypesAsync(listing, cancellationToken);

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

    private async Task<IReadOnlyCollection<ListingDocumentType>> ManagedEvidenceTypesAsync(Listing listing, CancellationToken cancellationToken)
    {
        var documents = await dbContext.ListingDocuments.AsNoTracking().Include(x => x.StoredFile)
            .Where(x => x.TenantId == listing.TenantId && x.ListingId == listing.Id && x.UploadedAt != null
                && x.StoredFile != null && x.StoredFile.TenantId == listing.TenantId && x.StoredFile.ListingId == listing.Id
                && !x.StoredFile.IsPhoto && x.StoredFile.DeletedAt == null).ToListAsync(cancellationToken);
        var available = new HashSet<ListingDocumentType>();
        foreach (var document in documents)
        {
            var file = document.StoredFile!;
            var bytes = await fileStorage.ReadAsync(file.StorageKey, cancellationToken);
            if (bytes is not null && bytes.LongLength == file.Length && Convert.ToHexString(SHA256.HashData(bytes)) == file.Sha256)
                available.Add(document.DocumentType);
        }
        return available;
    }

    private async Task ReviewAsync(Guid listingId, ListingComplianceStatus newStatus, string? notes, CancellationToken cancellationToken)
    {
        EnsurePlatformComplianceReviewer();

        var listing = await dbContext.Listings
            .Include(x => x.Compliance)
            .FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);

        if (listing is null)
        {
            throw new InvalidOperationException("Listing not found.");
        }

        var compliance = listing.Compliance ?? throw new InvalidOperationException("Listing compliance is required.");

        compliance.Status = newStatus;
        compliance.ReviewNotes = notes;
        compliance.ReviewedAt = DateTimeOffset.UtcNow;
        compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
        compliance.ReviewedByUserId = ((ITenantContextWithActor)tenantContext).ActorId;

        listing.IsActive = newStatus == ListingComplianceStatus.Approved;
        listing.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
