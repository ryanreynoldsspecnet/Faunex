using Faunex.Application.Auth;
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

[ApiController, Route("api/orders/{id:guid}/pricing"), Authorize(Roles = "Buyer")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(8192)]
public sealed class OrderPricingController(ApplicationDbContext db, ITenantContext tenant, TimeProvider clock) : ControllerBase
{
    private async Task<AuctionOrder> Own(Guid id, CancellationToken ct)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenant, "price buyer orders");
        ServiceAuthorization.EnsureRole(tenant, FaunexRoles.Buyer);
        var actor = (tenant as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException();
        return await db.AuctionOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId && x.BuyerId == actor, ct)
            ?? throw new KeyNotFoundException("Order not found.");
    }
    [HttpGet("options")]
    public async Task<ActionResult<ChargeTermsDto>> Options(Guid id, CancellationToken ct)
    {
        var order = await Own(id, ct);
        return order.ChargeTermsJson is { } json ? ChargePricing.Read<ChargeTermsDto>(json)
            : Conflict(new { error = "No published pricing terms were recorded for this auction. Tenant review required." });
    }
    [HttpPost("preview")]
    public async Task<ActionResult<PriceBreakdownDto>> Preview(Guid id, PriceSelectionRequest request, CancellationToken ct) =>
        ChargePricing.Calculate(await Own(id, ct), request);

    [HttpPost("finalise")]
    public async Task<ActionResult<OrderQuoteDto>> Finalise(Guid id, AcceptPriceRequest request, CancellationToken ct)
    {
        var order = await Own(id, ct);
        if (request.Selection is null) throw new ListingValidationException("Review a delivery and extras selection first.");
        var pricing = ChargePricing.Calculate(order, request.Selection);
        if (pricing.Total != request.ExpectedTotal) throw new ListingEditConflictException("The reviewed total does not match. Review the itemised total again.");
        var existing = await db.OrderPriceQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == id && x.TenantId == order.TenantId, ct);
        if (existing is not null)
        {
            if (existing.PricingJson != ChargePricing.Serialize(pricing)) throw new ListingEditConflictException("This order's final pricing has already been accepted and cannot be changed.");
            return ChargePricing.Quote(existing);
        }
        var now = clock.GetUtcNow();
        var quote = new OrderPriceQuote { TenantId = order.TenantId, OrderId = id, AcceptedBy = order.BuyerId,
            PricingJson = ChargePricing.Serialize(pricing), CreatedAt = now, UpdatedAt = now };
        db.OrderPriceQuotes.Add(quote); await db.SaveChangesAsync(ct); return ChargePricing.Quote(quote);
    }
}
