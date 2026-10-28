using System.Data;
using System.Security.Cryptography;
using Faunex.Api.Tenancy;
using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Faunex.Api.Files;

public sealed record FilePayload(byte[] Bytes, string ContentType, string FileName);

public sealed class ListingFileService(ApplicationDbContext db, ITenantContext tenant, TenantRequestContext origin,
    IListingFileStorage storage, IUploadScanner scanner, UploadContentValidator validator, ILogger<ListingFileService> logger)
{
    private Guid Actor => (tenant as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException();
    private bool Reviewer => tenant.IsPlatformAdmin && tenant is ITenantContextWithRoles r
        && r.Roles.Any(x => x is FaunexRoles.PlatformSuperAdmin or FaunexRoles.PlatformComplianceAdmin);

    private async Task<Listing> FindAsync(Guid id, bool write, bool tracked, CancellationToken ct)
    {
        _ = Actor;
        if (write || !Reviewer)
        {
            ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenant, "manage listing files");
            ServiceAuthorization.EnsureTenantAdminOrSeller(tenant);
        }
        var query = db.Listings.IgnoreQueryFilters().Include(x => x.Compliance).Where(x => x.Id == id);
        if (!Reviewer || write) query = query.Where(x => x.TenantId == tenant.TenantId);
        var listing = await (tracked ? query : query.AsNoTracking()).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Listing not found.");
        if (!Reviewer || write) ServiceAuthorization.EnsureSellerOrTenantAdminOwnsSellerId(tenant, listing.SellerId);
        return listing;
    }

    private async Task<bool> CanModifyAsync(Listing listing, CancellationToken ct) => !Reviewer
        && listing.Compliance?.Status is not (ListingComplianceStatus.UnderReview or ListingComplianceStatus.Suspended)
        && !await db.Auctions.IgnoreQueryFilters().AnyAsync(x => x.TenantId == listing.TenantId && x.ListingId == listing.Id, ct);

    private async Task EnsureEditableAsync(Listing listing, Guid revision, CancellationToken ct)
    {
        if (listing.Revision != revision) throw new ListingEditConflictException("This listing changed. Reload its files before continuing.");
        if (!await CanModifyAsync(listing, ct)) throw new ListingEditConflictException("Files cannot be changed during review, suspension or auction activity.");
    }

    public async Task<ListingFilesDto> ListAsync(Guid listingId, CancellationToken ct)
    {
        var listing = await FindAsync(listingId, false, false, ct);
        var rows = await db.ListingFiles.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.ListingId == listingId && x.TenantId == listing.TenantId && x.DeletedAt == null)
            .OrderBy(x => x.SortOrder).Select(x => new ListingFileDto(x.Id, x.FileName, x.ContentType, x.Length, x.IsPhoto,
                db.ListingDocuments.IgnoreQueryFilters().Where(d => d.StoredFileId == x.Id && d.TenantId == listing.TenantId && d.ListingId == listingId)
                    .Select(d => (ListingDocumentType?)d.DocumentType).FirstOrDefault(), x.CreatedAt)).ToListAsync(ct);
        return new(listingId, listing.Revision, listing.Compliance?.Status.ToString() ?? "Draft", await CanModifyAsync(listing, ct), rows, listing.Title);
    }

    public async Task<ListingFilesDto> UploadAsync(Guid listingId, Guid revision, IFormFile file, bool isPhoto, ListingDocumentType? documentType, CancellationToken ct)
    {
        var initial = await FindAsync(listingId, true, false, ct);
        await EnsureEditableAsync(initial, revision, ct);
        if (file.Length is < 1 or > FileUploadOptions.MaxUploadBytes) throw new FileSecurityException("Upload a non-empty file no larger than 10 MB.");
        if ((isPhoto && documentType.HasValue) || (!isPhoto && (documentType is null || documentType == ListingDocumentType.Unknown || !Enum.IsDefined(documentType.Value))))
            throw new FileSecurityException("Select a valid document category, or upload this file as a photo.");
        using var body = new MemoryStream();
        await using (var input = file.OpenReadStream())
        {
            var buffer = new byte[65536];
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (body.Length + count > FileUploadOptions.MaxUploadBytes) throw new FileSecurityException("File exceeds 10 MB.");
                await body.WriteAsync(buffer.AsMemory(0, count), ct);
            }
        }
        var original = body.ToArray();
        if (original.Length != file.Length) throw new FileSecurityException("File length is invalid.");
        var validated = await validator.ValidateAsync(original, file.FileName, file.ContentType, isPhoto, ct);
        await scanner.ScanAsync(original, ct);
        var key = Guid.NewGuid().ToString("N");
        var written = false;
        var committed = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var listing = await FindAsync(listingId, true, true, ct);
            await EnsureEditableAsync(listing, revision, ct);
            var live = db.ListingFiles.IgnoreQueryFilters().Where(x => x.TenantId == listing.TenantId && x.DeletedAt == null);
            if (await live.CountAsync(x => x.ListingId == listingId, ct) >= FileUploadOptions.MaxFilesPerListing
                || (isPhoto && await live.CountAsync(x => x.ListingId == listingId && x.IsPhoto, ct) >= FileUploadOptions.MaxPhotosPerListing))
                throw new FileSecurityException("This listing has reached its file or photo limit.");
            var used = await live.Select(x => (long?)x.Length).SumAsync(ct) ?? 0;
            if (used + validated.Bytes.Length > FileUploadOptions.MaxTenantBytes) throw new FileSecurityException("This tenant has reached its file storage limit.");
            var order = (await live.Where(x => x.ListingId == listingId).Select(x => (int?)x.SortOrder).MaxAsync(ct) ?? 0) + 1;
            var entity = new ListingFile
            {
                ListingId = listingId, TenantId = listing.TenantId, UploadedByUserId = Actor, IsPhoto = isPhoto,
                StorageKey = key, FileName = file.FileName, ContentType = validated.ContentType,
                Length = validated.Bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(validated.Bytes)),
                OriginalSha256 = Convert.ToHexString(SHA256.HashData(original)), SortOrder = order
            };
            await storage.WriteAsync(key, validated.Bytes, ct);
            written = true;
            db.ListingFiles.Add(entity);
            if (!isPhoto) db.ListingDocuments.Add(new ListingDocument
            {
                ListingId = listingId, TenantId = listing.TenantId, DocumentType = documentType!.Value,
                StoredFileId = entity.Id, UploadedAt = DateTimeOffset.UtcNow
            });
            InvalidateReview(listing);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            committed = true;
            logger.LogInformation("Listing file uploaded. actor_id={ActorId} tenant_id={TenantId} listing_id={ListingId} file_id={FileId}", Actor, listing.TenantId, listingId, entity.Id);
        }
        catch (Exception ex) when (SerializationFailure(ex)) { throw new ListingEditConflictException("Another listing change completed first. Reload and try again."); }
        catch (IOException) { throw new FileServiceUnavailableException("File storage is unavailable. Please try again."); }
        finally { if (written && !committed) await CleanupAsync(key); }
        return await ListAsync(listingId, ct);
    }

    public async Task<ListingFilesDto> DeleteAsync(Guid listingId, Guid fileId, Guid revision, CancellationToken ct)
    {
        string key;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var listing = await FindAsync(listingId, true, true, ct);
            await EnsureEditableAsync(listing, revision, ct);
            var file = await db.ListingFiles.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == fileId && x.ListingId == listingId
                && x.TenantId == listing.TenantId && x.DeletedAt == null, ct) ?? throw new KeyNotFoundException("File not found.");
            key = file.StorageKey;
            file.DeletedAt = DateTimeOffset.UtcNow;
            var document = await db.ListingDocuments.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.StoredFileId == fileId && x.TenantId == listing.TenantId, ct);
            if (document is not null) { document.UploadedAt = null; document.VerifiedByAdmin = false; document.VerifiedAt = null; document.VerifiedByUserId = null; }
            InvalidateReview(listing);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Listing file deleted. actor_id={ActorId} tenant_id={TenantId} listing_id={ListingId} file_id={FileId}", Actor, listing.TenantId, listingId, fileId);
        }
        catch (Exception ex) when (SerializationFailure(ex)) { throw new ListingEditConflictException("Another listing change completed first. Reload and try again."); }
        await CleanupAsync(key);
        return await ListAsync(listingId, ct);
    }

    public async Task<FilePayload> ReadPrivateAsync(Guid listingId, Guid fileId, CancellationToken ct)
    {
        var listing = await FindAsync(listingId, false, false, ct);
        var file = await db.ListingFiles.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId && x.ListingId == listingId
            && x.TenantId == listing.TenantId && x.DeletedAt == null, ct) ?? throw new KeyNotFoundException("File not found.");
        return await ReadVerifiedAsync(file, ct);
    }

    public async Task<FilePayload> ReadThumbnailAsync(Guid listingId, Guid fileId, CancellationToken ct)
    {
        var file = await ReadPrivateAsync(listingId, fileId, ct);
        if (file.ContentType is not ("image/png" or "image/jpeg")) throw new KeyNotFoundException("Photo preview not found.");
        return new(await validator.ThumbnailAsync(file.Bytes, ct), "image/jpeg", "preview.jpg");
    }

    public async Task<IReadOnlyList<Guid>> PublicPhotosAsync(Guid listingId, CancellationToken ct)
    {
        var tenantId = await origin.TenantIdAsync(ct);
        if (tenantId is null) throw new KeyNotFoundException("Marketplace not found.");
        return await PublicPhotoQuery(tenantId.Value).Where(x => x.ListingId == listingId).OrderBy(x => x.SortOrder).Select(x => x.Id).ToListAsync(ct);
    }
    public async Task<FilePayload> ReadPublicPhotoAsync(Guid fileId, CancellationToken ct)
    {
        var tenantId = await origin.TenantIdAsync(ct);
        if (tenantId is null) throw new KeyNotFoundException("Photo not found.");
        var file = await PublicPhotoQuery(tenantId.Value).SingleOrDefaultAsync(x => x.Id == fileId, ct) ?? throw new KeyNotFoundException("Photo not found.");
        return await ReadVerifiedAsync(file, ct);
    }
    private IQueryable<ListingFile> PublicPhotoQuery(Guid tenantId) => db.ListingFiles.IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.TenantId == tenantId && x.IsPhoto && x.DeletedAt == null && x.Listing != null && x.Listing.TenantId == tenantId
            && x.Listing.IsActive && x.Listing.Compliance != null && x.Listing.Compliance.TenantId == tenantId
            && x.Listing.Compliance.Status == ListingComplianceStatus.Approved);

    private async Task<FilePayload> ReadVerifiedAsync(ListingFile file, CancellationToken ct)
    {
        try
        {
            var bytes = await storage.ReadAsync(file.StorageKey, ct) ?? throw new FileServiceUnavailableException("This file is currently unavailable.");
            if (bytes.LongLength != file.Length || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(file.Sha256, StringComparison.Ordinal))
                throw new FileServiceUnavailableException("Stored file integrity check failed.");
            return new(bytes, file.ContentType, file.FileName);
        }
        catch (IOException) { throw new FileServiceUnavailableException("File storage is unavailable."); }
    }
    private static void InvalidateReview(Listing listing)
    {
        listing.IsActive = false;
        listing.UpdatedAt = DateTimeOffset.UtcNow;
        listing.Compliance ??= new ListingCompliance { ListingId = listing.Id, TenantId = listing.TenantId };
        listing.Compliance.Status = ListingComplianceStatus.Draft;
        listing.Compliance.LastUpdatedAt = DateTimeOffset.UtcNow;
    }
    private async Task CleanupAsync(string key)
    {
        try { await storage.DeleteAsync(key, CancellationToken.None); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { logger.LogWarning(ex, "Could not remove unused listing storage object {StorageKey}; reconciliation required.", key); }
    }
    private static bool SerializationFailure(Exception ex) => ex is PostgresException { SqlState: "40001" }
        || ex is DbUpdateException { InnerException: PostgresException { SqlState: "40001" } };
}
