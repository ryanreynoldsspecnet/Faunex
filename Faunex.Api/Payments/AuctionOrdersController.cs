using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Payments;

[ApiController, Route("api/orders"), Authorize(Roles = "Buyer,TenantAdmin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuctionOrdersController(ApplicationDbContext db, ITenantContext tenant, TimeProvider clock) : ControllerBase
{
    private IQueryable<AuctionOrder> Visible()
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenant, "access tenant orders");
        ServiceAuthorization.EnsureRole(tenant, FaunexRoles.Buyer, FaunexRoles.TenantAdmin);
        var query = db.AuctionOrders.AsNoTracking().Where(x => x.TenantId == tenant.TenantId);
        if (tenant is not ITenantContextWithRoles roles || !roles.Roles.Contains(FaunexRoles.TenantAdmin))
        {
            var actor = (tenant as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException();
            query = query.Where(x => x.BuyerId == actor);
        }
        return query;
    }
    private static AuctionOrderDto Map(AuctionOrder row, OrderPriceQuote? quote) => new(row.Id, row.AuctionId, row.ListingId, row.Title, row.Quantity,
        row.Amount, row.CurrencyCode, row.CreatedAt, PricingStatus: quote is null ? "ChargesNotFinalised" : "Finalised",
        Quote: quote is null ? null : ChargePricing.Quote(quote));
    private async Task<AuctionOrderDto> MapAsync(AuctionOrder row, CancellationToken ct) => Map(row,
        await db.OrderPriceQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == row.Id && x.TenantId == row.TenantId, ct));

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuctionOrderDto>>> List(string? search, int skip = 0, int take = 25, CancellationToken ct = default)
    {
        var query = Visible();
        if (search?.Length > 200) throw new ListingValidationException("Search is limited to 200 characters.");
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.Contains(search.Trim()));
        skip = Math.Max(0, skip); take = Math.Clamp(take, 1, 100);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToList();
        var quotes = await db.OrderPriceQuotes.AsNoTracking().Where(x => ids.Contains(x.OrderId)).ToDictionaryAsync(x => x.OrderId, ct);
        return new PagedResult<AuctionOrderDto>(rows.Select(x => Map(x, quotes.GetValueOrDefault(x.Id))).ToList(), total);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuctionOrderDto>> Get(Guid id, CancellationToken ct) =>
        await Visible().SingleOrDefaultAsync(x => x.Id == id, ct) is { } row ? await MapAsync(row, ct) : NotFound();

    // Recover an order for a pre-Section-6 result, without trusting buyer-supplied money or identity.
    [HttpPost("from-auction/{id:guid}"), Authorize(Roles = "Buyer")]
    public async Task<ActionResult<AuctionOrderDto>> FromAuction(Guid id, CancellationToken ct)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenant, "create a winner order");
        var actor = (tenant as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException();
        var auction = await db.Auctions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId
            && x.RulesVersion == 1 && x.State == AuctionState.Closed && x.Outcome == "Sold" && x.WinnerId == actor, ct)
            ?? throw new KeyNotFoundException("No completed winning auction was found for this account.");
        var existing = await Visible().SingleOrDefaultAsync(x => x.AuctionId == id, ct);
        if (existing is not null) return await MapAsync(existing, ct);
        var listing = await db.Listings.SingleOrDefaultAsync(x => x.Id == auction.ListingId && x.TenantId == auction.TenantId, ct)
            ?? throw new ListingEditConflictException("The auction listing requires reconciliation.");
        var bid = await db.Bids.SingleOrDefaultAsync(x => x.Id == auction.WinningBidId && x.TenantId == auction.TenantId, ct)
            ?? throw new ListingEditConflictException("The winning bid requires reconciliation.");
        var order = WinnerOrderFactory.Create(auction, listing, bid, auction.ClosedAt ?? clock.GetUtcNow());
        db.AuctionOrders.Add(order); await db.SaveChangesAsync(ct); return await MapAsync(order, ct);
    }
}
