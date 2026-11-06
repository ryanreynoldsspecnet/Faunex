using Faunex.Api.Marketplace;
using Faunex.Api.Auth;
using Faunex.Api.Email;
using Faunex.Api.Payments;
using Faunex.Api.Tenancy;
using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Controllers;

[ApiController, Route("api/fixed-price"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FixedPriceController(ApplicationDbContext db, ITenantContext context, TenantRequestContext origin,
    MarketplaceLotProjection marketplace, TimeProvider clock, UserManager<ApplicationUser> users, IEmailSender email,
    ILogger<FixedPriceController> logger, PayFastRuntime payments) : ControllerBase
{
    [HttpGet("lots/{id:guid}")]
    public async Task<ActionResult<FixedPriceAvailabilityDto>> Availability(Guid id, CancellationToken ct)
    {
        if (await origin.TenantIdAsync(ct) is not { } tenant) return NotFound();
        var lot = await FixedLots(tenant).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (lot is null) return NotFound();
        var reason = await Reserved(tenant).AnyAsync(x => x.ListingId == id, ct) ? "This lot is reserved."
            : lot.CurrencyCode != "ZAR" ? "Online checkout supports ZAR only."
            : !await CheckoutConfigured(tenant, lot.CurrencyCode, ct) ? "Checkout is not yet configured for this lot." : null;
        return new FixedPriceAvailabilityDto(id, lot.Revision, lot.BuyNowPrice!.Value, lot.CurrencyCode, lot.Quantity, reason is null, reason);
    }

    [HttpPost("lots/{id:guid}/buy"), Authorize(Policy = "BuyerOnly")]
    public async Task<ActionResult<FixedPriceOrderReference>> Buy(Guid id, FixedPricePurchaseRequest request, CancellationToken ct)
    {
        var (tenant, buyer) = Actor();
        var existing = await Reserved(tenant).AsNoTracking().SingleOrDefaultAsync(x => x.ListingId == id, ct);
        if (existing is not null)
        {
            if (existing.BuyerId == buyer && existing.FixedPriceOfferId == null
                && existing.Amount == request.ExpectedPrice && existing.CurrencyCode == request.CurrencyCode)
                return new FixedPriceOrderReference(existing.Id);
            throw Unavailable();
        }
        var lot = await PurchaseLot(tenant, id, ct);
        ValidateTerms(lot, buyer, request.Revision, request.CurrencyCode);
        if (lot.BuyNowPrice != request.ExpectedPrice) throw Changed();
        var order = await Reserve(lot, buyer, request.ExpectedPrice, null, ct);
        await db.SaveChangesAsync(ct);
        return new FixedPriceOrderReference(order.Id);
    }

    [HttpPost("lots/{id:guid}/offers"), Authorize(Policy = "BuyerOnly"), EnableRateLimiting("fixed-price-offers")]
    public async Task<ActionResult<FixedPriceOfferDto>> MakeOffer(Guid id, FixedPriceOfferRequest request, CancellationToken ct)
    {
        var (tenant, buyer) = Actor();
        var lot = await PurchaseLot(tenant, id, ct);
        ValidateTerms(lot, buyer, request.Revision, request.CurrencyCode);
        if (!await CheckoutConfigured(tenant, lot.CurrencyCode, ct)) throw CheckoutUnavailable();
        if (!TimedAuctionRules.Money(request.Amount) || request.Amount >= lot.BuyNowPrice)
            throw new ListingValidationException("Enter a positive offer below the listed whole-lot price, with up to two decimal places.");
        var now = clock.GetUtcNow();
        var pending = await db.FixedPriceOffers.SingleOrDefaultAsync(x => x.TenantId == tenant && x.ListingId == id
            && x.BuyerId == buyer && (x.Status == "Pending" || x.Status == "Countered"), ct);
        if (pending is not null)
        {
            if (pending.ExpiresAt > now && pending.ListingRevision == lot.Revision)
            {
                if (pending.Amount == request.Amount && pending.Status == "Pending") return Map(pending, "Pending", null);
                throw new ListingEditConflictException("Resolve your open offer before making a new one.");
            }
            Change(pending, pending.ExpiresAt <= now ? "Expired" : "Unavailable");
            await db.SaveChangesAsync(ct);
        }
        var offer = new FixedPriceOffer { TenantId = tenant, ListingId = id, ListingRevision = lot.Revision, BuyerId = buyer,
            SellerId = lot.SellerId, Title = lot.Title, Amount = request.Amount, CurrencyCode = lot.CurrencyCode,
            CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddHours(48) };
        db.FixedPriceOffers.Add(offer);
        await db.SaveChangesAsync(ct);
        await Notify(tenant, null, "New marketplace offer", "/tenant/offers");
        return StatusCode(StatusCodes.Status201Created, Map(offer, offer.Status, null));
    }

    [HttpGet("offers"), Authorize(Policy = "BuyerOnly")]
    public Task<PagedResult<FixedPriceOfferDto>> Mine(int skip = 0, int take = 25, CancellationToken ct = default)
    {
        var (tenant, buyer) = Actor();
        return Offers(db.FixedPriceOffers.AsNoTracking().Where(x => x.TenantId == tenant && x.BuyerId == buyer), tenant, true, skip, take, ct);
    }

    [HttpGet("incoming"), Authorize(Policy = "ListingManager")]
    public Task<PagedResult<FixedPriceOfferDto>> Incoming(int skip = 0, int take = 25, CancellationToken ct = default)
    {
        var (tenant, seller) = Actor();
        return Offers(db.FixedPriceOffers.AsNoTracking().Where(x => x.TenantId == tenant && (IsAdmin || x.SellerId == seller)), tenant, IsAdmin, skip, take, ct);
    }

    [HttpPost("offers/{id:guid}/withdraw"), Authorize(Policy = "BuyerOnly")]
    public async Task<IActionResult> Withdraw(Guid id, FixedPriceOfferDecision request, CancellationToken ct)
    {
        var (tenant, buyer) = Actor();
        var offer = await db.FixedPriceOffers.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id && x.BuyerId == buyer, ct);
        if (offer is null) return NotFound();
        RequireOpen(offer, request.Revision, "Pending");
        Change(offer, "Withdrawn");
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("incoming/{id:guid}/decision"), Authorize(Policy = "ListingManager")]
    public async Task<IActionResult> Decide(Guid id, FixedPriceOfferDecision request, CancellationToken ct)
    {
        var (tenant, seller) = Actor();
        var offer = await db.FixedPriceOffers.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id
            && (IsAdmin || x.SellerId == seller), ct);
        if (offer is null) return NotFound();
        RequireOpen(offer, request.Revision, "Pending");
        if (request.Decision == "accept")
        {
            var lot = await PurchaseLot(tenant, offer.ListingId, ct);
            ValidateTerms(lot, offer.BuyerId, offer.ListingRevision, offer.CurrencyCode);
            if (lot.SellerId != offer.SellerId || offer.Amount >= lot.BuyNowPrice || !TimedAuctionRules.Money(offer.Amount)) throw Changed();
            Change(offer, "Accepted");
            await Reserve(lot, offer.BuyerId, offer.Amount, offer.Id, ct);
        }
        else if (request.Decision == "decline") Change(offer, "Declined");
        else if (request.Decision == "counter")
        {
            var lot = await PurchaseLot(tenant, offer.ListingId, ct);
            ValidateTerms(lot, offer.BuyerId, offer.ListingRevision, offer.CurrencyCode);
            if (lot.SellerId != offer.SellerId) throw Changed();
            if (request.CounterAmount is not { } amount || !TimedAuctionRules.Money(amount) || amount <= offer.Amount || amount > lot.BuyNowPrice)
                throw new ListingValidationException("Counter above the buyer's offer and at or below the listed price.");
            Change(offer, "Countered");
            offer.CounterAmount = amount;
            offer.ExpiresAt = clock.GetUtcNow().AddHours(48);
        }
        else throw new ListingValidationException("Choose accept, counter or decline.");
        await db.SaveChangesAsync(ct);
        await Notify(tenant, offer.BuyerId, "Marketplace offer updated", "/buyer/offers");
        return NoContent();
    }

    [HttpPost("offers/{id:guid}/decision"), Authorize(Policy = "BuyerOnly")]
    public async Task<ActionResult<FixedPriceOrderReference>> DecideCounter(Guid id, FixedPriceOfferDecision request, CancellationToken ct)
    {
        var (tenant, buyer) = Actor();
        var offer = await db.FixedPriceOffers.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id && x.BuyerId == buyer, ct);
        if (offer is null) return NotFound();
        if (request.Decision == "accept" && offer.Status == "Accepted" &&
            await Reserved(tenant).AsNoTracking().SingleOrDefaultAsync(x => x.BuyerId == buyer && x.FixedPriceOfferId == id, ct) is { } existing)
            return new FixedPriceOrderReference(existing.Id);
        RequireOpen(offer, request.Revision, "Countered");
        if (request.Decision == "decline")
        {
            Change(offer, "CounterDeclined");
            await db.SaveChangesAsync(ct);
            return NoContent();
        }
        if (request.Decision != "accept") throw new ListingValidationException("Choose accept or decline.");
        var lot = await PurchaseLot(tenant, offer.ListingId, ct);
        ValidateTerms(lot, buyer, offer.ListingRevision, offer.CurrencyCode);
        if (lot.SellerId != offer.SellerId || offer.CounterAmount is not { } amount || !TimedAuctionRules.Money(amount)
            || amount <= offer.Amount || amount > lot.BuyNowPrice) throw Changed();
        Change(offer, "Accepted");
        var order = await Reserve(lot, buyer, amount, offer.Id, ct);
        await db.SaveChangesAsync(ct);
        return new FixedPriceOrderReference(order.Id);
    }

    [HttpPost("orders/{id:guid}/release"), Authorize(Roles = "Buyer,TenantAdmin")]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct)
    {
        var (tenant, actor) = Actor();
        var order = await db.AuctionOrders.SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id
            && x.AuctionId == null && (IsAdmin || x.BuyerId == actor), ct);
        if (order is null) return NotFound();
        if (order.ReleasedAt != null) return NoContent();
        if (order.CheckoutStarted || await db.PayFastAttempts.AnyAsync(x => x.OrderId == id && x.TenantId == tenant, ct)
            || await db.OrderPriceQuotes.AnyAsync(x => x.OrderId == id && x.TenantId == tenant, ct))
            throw new ListingEditConflictException("This reservation needs payment review before release.");
        order.ReleasedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private IQueryable<Listing> FixedLots(Guid tenant) => marketplace.Visible(tenant, includeReserved: true)
        .Where(x => x.BuyNowPrice > 0 && x.Quantity > 0 && (x.CurrencyCode == "ZAR" || x.CurrencyCode == "USD")
            && !db.Auctions.IgnoreQueryFilters().Any(a => a.TenantId == tenant && a.ListingId == x.Id));
    private IQueryable<AuctionOrder> Reserved(Guid tenant) => db.AuctionOrders.IgnoreQueryFilters()
        .Where(x => x.TenantId == tenant && x.AuctionId == null && x.ReleasedAt == null);
    private async Task<Listing> PurchaseLot(Guid tenant, Guid id, CancellationToken ct)
    {
        var lot = await FixedLots(tenant).AsTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (lot is null || await Reserved(tenant).AnyAsync(x => x.ListingId == id, ct)) throw Unavailable();
        return lot;
    }

    private static void ValidateTerms(Listing lot, Guid buyer, Guid revision, string currency)
    {
        if (lot.SellerId == buyer) throw new ListingValidationException("You cannot buy or make an offer on your own lot.");
        if (lot.CurrencyCode != "ZAR") throw new ListingValidationException("Online checkout supports ZAR only.");
        if (lot.Revision != revision || lot.CurrencyCode != currency || !TimedAuctionRules.Money(lot.BuyNowPrice!.Value)) throw Changed();
    }

    private async Task<bool> CheckoutConfigured(Guid tenant, string currency, CancellationToken ct) =>
        await HasSandboxMerchant(tenant, ct) && await db.TenantChargeCatalogs.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenant && x.IsPublished && x.CurrencyCode == currency, ct);

    private async Task<bool> HasSandboxMerchant(Guid tenant, CancellationToken ct) => payments.Ready &&
        await db.TenantPaymentSettings.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenant && x.ProtectedCredentials != "", ct);

    private async Task<AuctionOrder> Reserve(Listing lot, Guid buyer, decimal amount, Guid? offerId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!await HasSandboxMerchant(lot.TenantId, ct)) throw CheckoutUnavailable();
        var catalog = await db.TenantChargeCatalogs.SingleOrDefaultAsync(x => x.TenantId == lot.TenantId
            && x.IsPublished && x.CurrencyCode == lot.CurrencyCode, ct) ?? throw CheckoutUnavailable();
        // The listing revision serializes reservation against changes to the advertised lot.
        lot.Revision = Guid.NewGuid();
        var order = new AuctionOrder { TenantId = lot.TenantId, ListingId = lot.Id, BuyerId = buyer, SellerId = lot.SellerId,
            FixedPriceOfferId = offerId, Title = lot.Title, Quantity = lot.Quantity, Amount = amount,
            CurrencyCode = lot.CurrencyCode, CreatedAt = now, UpdatedAt = now, ChargeTermsJson = ChargePricing.Freeze(catalog, lot.CurrencyCode) };
        db.AuctionOrders.Add(order);
        return order;
    }

    private async Task<PagedResult<FixedPriceOfferDto>> Offers(IQueryable<FixedPriceOffer> query, Guid tenant, bool showOrder,
        int skip, int take, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Status == "Pending" || x.Status == "Countered")
            .ThenBy(x => x.Id).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 50)).ToListAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var listingIds = rows.Select(x => x.ListingId).Distinct().ToArray();
        var revisions = await FixedLots(tenant).Where(x => listingIds.Contains(x.Id) && !Reserved(tenant).Any(o => o.ListingId == x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Revision, ct);
        var orders = await db.AuctionOrders.AsNoTracking().Where(x => x.TenantId == tenant && x.FixedPriceOfferId.HasValue && ids.Contains(x.FixedPriceOfferId.Value))
            .ToDictionaryAsync(x => x.FixedPriceOfferId!.Value, ct);
        var now = clock.GetUtcNow();
        return new(rows.Select(x =>
        {
            var order = orders.GetValueOrDefault(x.Id);
            var status = x.Status is not ("Pending" or "Countered") ? order?.ReleasedAt is not null ? "Released" : x.Status
                : x.ExpiresAt <= now ? "Expired" : revisions.GetValueOrDefault(x.ListingId) != x.ListingRevision ? "Unavailable" : x.Status;
            return Map(x, status, showOrder ? order?.Id : null);
        }).ToList(), total);
    }

    private (Guid Tenant, Guid Actor) Actor()
    {
        if (context.IsPlatformAdmin || context.TenantId is not { } tenant || context is not ITenantContextWithActor { ActorId: { } actor })
            throw new UnauthorizedAccessException();
        return (tenant, actor);
    }
    private bool IsAdmin => context is ITenantContextWithRoles roles && roles.Roles.Contains(FaunexRoles.TenantAdmin);
    private static FixedPriceOfferDto Map(FixedPriceOffer x, string status, Guid? order) =>
        new(x.Id, x.ListingId, x.Title, x.Amount, x.CurrencyCode, status, x.ExpiresAt, x.Revision, order, x.CounterAmount);
    private void RequireOpen(FixedPriceOffer offer, Guid revision, string expectedStatus)
    {
        if (offer.Revision != revision || offer.Status != expectedStatus || offer.ExpiresAt <= clock.GetUtcNow())
            throw new ListingEditConflictException("This offer is no longer open. Refresh offers.");
    }
    private void Change(FixedPriceOffer offer, string status)
    {
        offer.Status = status; offer.Revision = Guid.NewGuid(); offer.UpdatedAt = clock.GetUtcNow();
    }
    private static ListingEditConflictException Changed() => new("The lot terms changed. Review the current listing.");
    private static ListingEditConflictException Unavailable() => new("This fixed-price lot is no longer available.");
    private static ListingEditConflictException CheckoutUnavailable() => new("Checkout is not yet configured for this lot.");

    private async Task Notify(Guid tenant, Guid? buyer, string subject, string path)
    {
        if (origin.Host is not { } host) return;
        try
        {
            var recipients = buyer is { } id
                ? new[] { await users.FindByIdAsync(id.ToString()) }
                : (await users.GetUsersInRoleAsync(FaunexRoles.TenantAdmin)).ToArray();
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            foreach (var user in recipients.Where(x => x is not null && x.TenantId == tenant && !x.IsPlatformAdmin
                && (x.LockoutEnd == null || x.LockoutEnd <= clock.GetUtcNow()) && !string.IsNullOrWhiteSpace(x.Email)))
            {
                try { await email.SendMarketplaceNoticeAsync(user!.Email!, subject, $"Open your marketplace offers: https://{host}{path}", stop.Token); }
                catch (Exception ex) when (ex is EmailDeliveryException or FormatException or OperationCanceledException)
                { logger.LogWarning("A marketplace offer email could not be delivered; the offer remains in the inbox."); }
                if (stop.IsCancellationRequested) break;
            }
        }
        catch (Exception)
        {
            // A committed offer remains in the inbox even if email delivery fails.
            logger.LogWarning("Marketplace offer notification could not be sent for tenant {TenantId}", tenant);
        }
    }
}
