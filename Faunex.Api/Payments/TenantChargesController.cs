using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Payments;

[ApiController, Route("api/tenant/charges"), Authorize(Policy = "TenantAdminOnly")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(65536)]
public sealed class TenantChargesController(ApplicationDbContext db, ITenantContext tenant, TimeProvider clock) : ControllerBase
{
    private Guid TenantId()
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenant, "manage tenant charges");
        ServiceAuthorization.EnsureTenantAdmin(tenant);
        return tenant.TenantId!.Value;
    }
    private static ChargeCatalogDto Map(TenantChargeCatalog? row) => row is null ? new(Guid.Empty, false, "ZAR", "", [])
        : new(row.Revision, row.IsPublished, row.CurrencyCode, row.Terms, ChargePricing.Read<List<ChargeOptionDto>>(row.OptionsJson));

    [HttpGet]
    public async Task<ActionResult<ChargeCatalogDto>> Get(CancellationToken ct)
    {
        var id = TenantId(); return Map(await db.TenantChargeCatalogs.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == id, ct));
    }

    [HttpPut]
    public async Task<ActionResult<ChargeCatalogDto>> Save(ChargeCatalogDto request, CancellationToken ct)
    {
        var id = TenantId();
        ChargePricing.Validate(request.CurrencyCode, request.Terms, request.Options, request.IsPublished);
        var row = await db.TenantChargeCatalogs.SingleOrDefaultAsync(x => x.TenantId == id, ct);
        if (request.Revision != (row?.Revision ?? Guid.Empty)) throw new ListingEditConflictException("Charge options changed. Reload before saving.");
        var previousIds = row is null ? new HashSet<Guid>() : ChargePricing.Read<List<ChargeOptionDto>>(row.OptionsJson).Select(x => x.Id).ToHashSet();
        var suppliedIds = request.Options.Where(x => x.Id != Guid.Empty).Select(x => x.Id).ToList();
        if (suppliedIds.Distinct().Count() != suppliedIds.Count || suppliedIds.Any(x => !previousIds.Contains(x)))
            throw new ListingValidationException("Option identifiers must belong to the current catalogue. New options must not supply an identifier.");
        var options = request.Options.Select(x => x with { Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id, Name = x.Name.Trim(), Description = x.Description.Trim() }).ToList();
        if (row is null) { row = new TenantChargeCatalog { TenantId = id }; db.TenantChargeCatalogs.Add(row); }
        row.OptionsJson = ChargePricing.Serialize(options); row.Terms = request.Terms.Trim(); row.CurrencyCode = request.CurrencyCode;
        row.IsPublished = request.IsPublished; row.Revision = Guid.NewGuid(); row.WriteStamp = Guid.NewGuid();
        row.UpdatedBy = (tenant as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException(); row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct); return Map(row);
    }
}
