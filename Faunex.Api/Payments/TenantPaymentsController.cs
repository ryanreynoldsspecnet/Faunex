using System.Text.Json;
using System.Text.RegularExpressions;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Payments;

[ApiController, Route("api/tenant/payments/payfast"), Authorize(Policy = "TenantAdminOnly")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(8192)]
public sealed class TenantPaymentsController(ApplicationDbContext db, ITenantContext tenant, IPaymentSecretProtector secrets, TimeProvider clock, PayFastRuntime runtime) : ControllerBase
{
    private Guid TenantId()
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenant, "manage tenant payment settings");
        ServiceAuthorization.EnsureTenantAdmin(tenant);
        return tenant.TenantId!.Value;
    }
    private PayFastSettingsDto Map(TenantPaymentSettings? row) => new(row?.MerchantId ?? "", row?.ProtectedCredentials.Length > 0,
        row?.Revision ?? Guid.Empty, row?.UpdatedAt, CheckoutAvailable: runtime.Ready && row?.ProtectedCredentials.Length > 0);

    [HttpGet]
    public async Task<ActionResult<PayFastSettingsDto>> Get(CancellationToken ct)
    {
        var id = TenantId();
        return Map(await db.TenantPaymentSettings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == id, ct));
    }

    [HttpPut]
    public async Task<ActionResult<PayFastSettingsDto>> Save(SavePayFastSettingsRequest request, CancellationToken ct)
    {
        var id = TenantId();
        if (request.Environment != "Sandbox") throw new ListingValidationException("Live payments are not enabled in this section.");
        if (!Regex.IsMatch(request.MerchantId ?? "", "^[0-9]{8}$", RegexOptions.CultureInvariant))
            throw new ListingValidationException("Enter the eight-digit PayFast sandbox merchant ID.");
        var row = await db.TenantPaymentSettings.SingleOrDefaultAsync(x => x.TenantId == id, ct);
        if (request.Revision != (row?.Revision ?? Guid.Empty)) throw new ListingEditConflictException("Payment settings changed. Reload before saving.");
        var replace = !string.IsNullOrEmpty(request.MerchantKey) || !string.IsNullOrEmpty(request.Passphrase);
        if (row?.ProtectedCredentials.Length is not > 0 || row.MerchantId != request.MerchantId) replace = true;
        if (replace && (!Regex.IsMatch(request.MerchantKey ?? "", "^[a-zA-Z0-9]{13}$", RegexOptions.CultureInvariant)
            || !Regex.IsMatch(request.Passphrase ?? "", "^[a-zA-Z0-9_/-]{1,32}$", RegexOptions.CultureInvariant)))
            throw new ListingValidationException("Supply a 13-character merchant key and a 1-32 character passphrase (letters, digits, -, _ or /). Replacing credentials requires both fields.");
        if (row is null) { row = new TenantPaymentSettings { TenantId = id }; db.TenantPaymentSettings.Add(row); }
        if (replace) row.ProtectedCredentials = secrets.Protect(id, JsonSerializer.Serialize(new { MerchantKey = request.MerchantKey, Passphrase = request.Passphrase }));
        row.MerchantId = request.MerchantId!;
        row.Revision = Guid.NewGuid(); row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedBy = (tenant as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException();
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(Guid revision, CancellationToken ct)
    {
        var id = TenantId();
        var row = await db.TenantPaymentSettings.SingleOrDefaultAsync(x => x.TenantId == id, ct) ?? throw new KeyNotFoundException();
        if (revision != row.Revision) throw new ListingEditConflictException("Payment settings changed. Reload before removing credentials.");
        row.ProtectedCredentials = ""; row.MerchantId = ""; row.Revision = Guid.NewGuid(); row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedBy = (tenant as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException();
        await db.SaveChangesAsync(ct); return NoContent();
    }
}
