using Faunex.Api.Payments;
using Faunex.Application.DTOs;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Marketplace;

public sealed class FixedPriceInventory(ApplicationDbContext db, MarketplaceLotProjection marketplace, PayFastRuntime payments, TimeProvider clock)
{
    public IQueryable<Listing> Lots(Guid tenant) => marketplace.Visible(tenant, includeReserved: true)
        .Where(x => x.BuyNowPrice > 0 && x.Quantity > 0 && (x.CurrencyCode == "ZAR" || x.CurrencyCode == "USD")
            && !db.Auctions.IgnoreQueryFilters().Any(a => a.TenantId == tenant && a.ListingId == x.Id));

    public IQueryable<AuctionOrder> Reserved(Guid tenant) => db.AuctionOrders.IgnoreQueryFilters()
        .Where(x => x.TenantId == tenant && x.AuctionId == null && x.ReleasedAt == null);

    public async Task<bool> CheckoutConfiguredAsync(Guid tenant, string currency, CancellationToken ct) =>
        await HasSandboxMerchantAsync(tenant, ct) && await db.TenantChargeCatalogs.IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenant && x.IsPublished && x.CurrencyCode == currency, ct);

    public async Task<string> FreezeChargesAsync(Guid tenant, string currency, CancellationToken ct)
    {
        if (!await HasSandboxMerchantAsync(tenant, ct)) throw CheckoutUnavailable();
        var catalog = await db.TenantChargeCatalogs.SingleOrDefaultAsync(x => x.TenantId == tenant
            && x.IsPublished && x.CurrencyCode == currency, ct) ?? throw CheckoutUnavailable();
        return ChargePricing.Freeze(catalog, currency)!;
    }

    public AuctionOrder Reserve(Listing lot, Guid buyer, decimal amount, Guid? offerId, string charges)
    {
        var now = clock.GetUtcNow();
        // Serialize checkout against listing edits and other reservations.
        lot.Revision = Guid.NewGuid();
        var order = new AuctionOrder { TenantId = lot.TenantId, ListingId = lot.Id, BuyerId = buyer, SellerId = lot.SellerId,
            FixedPriceOfferId = offerId, Title = lot.Title, Quantity = lot.Quantity, Amount = amount,
            CurrencyCode = lot.CurrencyCode, CreatedAt = now, UpdatedAt = now, ChargeTermsJson = charges };
        db.AuctionOrders.Add(order);
        return order;
    }

    private async Task<bool> HasSandboxMerchantAsync(Guid tenant, CancellationToken ct) => payments.Ready &&
        await db.TenantPaymentSettings.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenant && x.ProtectedCredentials != "", ct);

    public static ListingEditConflictException CheckoutUnavailable() => new("Checkout is not yet configured for this marketplace.");
}
