using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Controllers.Tenant;

[ApiController, Route("api/tenant/collections"), Authorize(Policy = "TenantAdminOnly")]
public sealed class TenantAuctionCollectionsController(ApplicationDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AuctionCollectionDto>>> Get(CancellationToken ct) =>
        await OptionsAsync(TenantId(), ct);

    [HttpPost]
    public async Task<ActionResult<AuctionCollectionDto>> Create(CreateAuctionCollectionRequest request, CancellationToken ct)
    {
        var tenantId = TenantId();
        var name = await ValidateNameAsync(tenantId, request.Name, null, ct);
        var row = new AuctionCollection { TenantId = tenantId, Name = name, NormalizedName = name.ToUpperInvariant() };
        db.AuctionCollections.Add(row);
        await SaveAsync(ct);
        return StatusCode(StatusCodes.Status201Created, Map(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AuctionCollectionDto>> Update(Guid id, UpdateAuctionCollectionRequest request, CancellationToken ct)
    {
        var tenantId = TenantId();
        var row = await db.AuctionCollections.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (row is null) return NotFound();
        if (row.Revision != request.Revision) throw new ListingEditConflictException("This collection changed. Reload before saving.");
        row.Name = await ValidateNameAsync(tenantId, request.Name, id, ct);
        row.NormalizedName = row.Name.ToUpperInvariant();
        row.IsActive = request.IsActive;
        row.Revision = Guid.NewGuid();
        await SaveAsync(ct);
        return Map(row);
    }

    [HttpGet("lots/{listingId:guid}")]
    public async Task<ActionResult<LotCollectionDto>> GetAssignment(Guid listingId, CancellationToken ct)
    {
        var tenantId = TenantId();
        var listing = await db.Listings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == listingId, ct);
        if (listing is null) return NotFound();
        return new LotCollectionDto(listing.AuctionCollectionId, listing.Revision, await OptionsAsync(tenantId, ct));
    }

    [HttpPut("lots/{listingId:guid}")]
    public async Task<ActionResult<LotCollectionDto>> Assign(Guid listingId, AssignLotCollectionRequest request, CancellationToken ct)
    {
        var tenantId = TenantId();
        var listing = await db.Listings.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == listingId, ct);
        if (listing is null) return NotFound();
        if (listing.Revision != request.Revision) throw new ListingEditConflictException("This lot changed. Reload before saving.");
        if (request.CollectionId is { } id && id != listing.AuctionCollectionId &&
            !await db.AuctionCollections.AnyAsync(x => x.TenantId == tenantId && x.Id == id && x.IsActive, ct))
            throw new ListingValidationException("Choose an active collection in this marketplace.");

        listing.AuctionCollectionId = request.CollectionId;
        await db.SaveChangesAsync(ct);
        return new LotCollectionDto(listing.AuctionCollectionId, listing.Revision, await OptionsAsync(tenantId, ct));
    }

    private Guid TenantId()
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenant, "manage auction collections");
        ServiceAuthorization.EnsureTenantAdmin(tenant);
        return tenant.TenantId!.Value;
    }

    private Task<List<AuctionCollectionDto>> OptionsAsync(Guid tenantId, CancellationToken ct) =>
        db.AuctionCollections.AsNoTracking().Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsActive).ThenBy(x => x.NormalizedName)
            .Select(x => new AuctionCollectionDto(x.Id, x.Name, x.IsActive, x.Revision)).ToListAsync(ct);

    private async Task<string> ValidateNameAsync(Guid tenantId, string? input, Guid? except, CancellationToken ct)
    {
        var name = input?.Trim() ?? "";
        if (name.Length is < 1 or > 80 || name.Any(char.IsControl))
            throw new ListingValidationException("Enter a collection name of 1 to 80 characters.");
        var normalized = name.ToUpperInvariant();
        if (await db.AuctionCollections.AnyAsync(x => x.TenantId == tenantId && x.Id != except && x.NormalizedName == normalized, ct))
            throw new ListingEditConflictException("A collection with this name already exists.");
        return name;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
            { SqlState: "23505", ConstraintName: "IX_AuctionCollections_TenantId_NormalizedName" })
        {
            throw new ListingEditConflictException("A collection with this name already exists.");
        }
    }

    private static AuctionCollectionDto Map(AuctionCollection row) => new(row.Id, row.Name, row.IsActive, row.Revision);
}
