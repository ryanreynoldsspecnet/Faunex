using System.Net;
using System.Net.Http.Json;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Application.Tenancy;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class BuyerCartTests
{
    private const string Path = "/api/buyer/cart";

    [Fact]
    public async Task Cart_is_buyer_and_tenant_scoped_and_accepts_only_available_fixed_price_lots()
    {
        await using var test = await FileCase.CreateAsync();
        using var admin = await FixedPriceTests.Client(test, FaunexRoles.TenantAdmin);
        using var buyer = await FixedPriceTests.Client(test, FaunexRoles.Buyer);
        using var other = await FixedPriceTests.Client(test, FaunexRoles.Buyer);
        await test.SetState(ListingComplianceStatus.Approved, true);
        await FixedPriceTests.Configure(test, admin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await test.App.Client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await buyer.PutAsJsonAsync($"{Path}/lots/{test.Id}", new { })).StatusCode);
        await FixedPriceTests.MakeFixed(test);
        var first = await Add(buyer, test.Id);
        Assert.Single(first.Lots);
        Assert.Equal(first.Revision, (await Add(buyer, test.Id)).Revision);
        Assert.Empty((await Read(other)).Lots);

        var foreignTenant = await test.App.AddTenantAsync();
        using var foreign = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(foreignTenant, FaunexRoles.Buyer)));
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.PutAsJsonAsync($"{Path}/lots/{test.Id}", new { })).StatusCode);
        Assert.Empty((await Read(foreign)).Lots);
        buyer.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader);
        buyer.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(foreignTenant));
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync(Path)).StatusCode);
        buyer.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader);
        buyer.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(test.Tenant));

        Assert.Equal(HttpStatusCode.Conflict, (await buyer.DeleteAsync($"{Path}/lots/{test.Id}?revision={Guid.Empty}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await buyer.DeleteAsync($"{Path}/lots/{test.Id}?revision={first.Revision}")).StatusCode);
        Assert.Empty((await Read(buyer)).Lots);
        Assert.Empty((await buyer.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders"))!.Items);
    }

    [Fact]
    public async Task Checkout_revalidates_terms_and_persists_atomic_orders_and_retry_receipt()
    {
        await using var test = await FileCase.CreateAsync();
        using var admin = await FixedPriceTests.Client(test, FaunexRoles.TenantAdmin);
        using var buyer = await FixedPriceTests.Client(test, FaunexRoles.Buyer);
        await FixedPriceTests.MakeFixed(test);
        await FixedPriceTests.Configure(test, admin);
        var second = await AddLot(test, 750m);
        await Add(buyer, test.Id);
        var cart = await Add(buyer, second);
        var original = Review(cart);
        Assert.Equal(2, cart.Lots.Count);

        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lot = await db.Listings.IgnoreQueryFilters().SingleAsync(x => x.Id == second);
            lot.BuyNowPrice = 800m;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.PostAsJsonAsync(Path + "/checkout", original)).StatusCode);
        Assert.Empty((await buyer.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders"))!.Items);
        var reviewed = Review(await Read(buyer));
        var result = await Checkout(buyer, reviewed);
        Assert.Equal(2, result.Orders.Count);
        Assert.Equal(1300m, result.Orders.Sum(x => x.Subtotal));
        var replay = await Checkout(buyer, reviewed with { Lots = reviewed.Lots.Reverse().ToArray() });
        Assert.Equal(result.Orders.ToArray(), replay.Orders.ToArray());
        var different = reviewed with { Lots = [reviewed.Lots[0]] };
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.PostAsJsonAsync(Path + "/checkout", different)).StatusCode);

        var persisted = await Read(buyer);
        Assert.Empty(persisted.Lots);
        Assert.Equal(result.Orders.ToArray(), persisted.LatestCheckout!.Orders.ToArray());
        foreach (var row in result.Orders)
        {
            var terms = (await buyer.GetFromJsonAsync<ChargeTermsDto>($"/api/orders/{row.OrderId}/pricing/options"))!;
            var selection = new PriceSelectionRequest(terms.Revision, terms.Options.Single(x => x.Kind == "Delivery").Id,
                [terms.Options.Single(x => x.Kind == "OptionalExtra").Id]);
            var quote = await buyer.PostAsJsonAsync($"/api/orders/{row.OrderId}/pricing/finalise", new AcceptPriceRequest(selection, row.Subtotal + 75m));
            quote.EnsureSuccessStatusCode();
            var checkout = await buyer.PostAsJsonAsync($"/api/payments/payfast/orders/{row.OrderId}/checkout", new { });
            checkout.EnsureSuccessStatusCode();
            Assert.Equal(row.Subtotal + 75m, (await checkout.Content.ReadFromJsonAsync<PayFastCheckoutDto>())!.Amount);
        }
    }

    [Fact]
    public async Task Unavailable_lot_remains_removable_and_does_not_block_other_selected_lots()
    {
        await using var test = await FileCase.CreateAsync();
        using var admin = await FixedPriceTests.Client(test, FaunexRoles.TenantAdmin);
        using var buyer = await FixedPriceTests.Client(test, FaunexRoles.Buyer);
        await FixedPriceTests.MakeFixed(test);
        await FixedPriceTests.Configure(test, admin);
        var second = await AddLot(test, 750m);
        await Add(buyer, test.Id);
        await Add(buyer, second);
        await test.SetState(ListingComplianceStatus.Suspended, false);
        var cart = await Read(buyer);
        var hidden = cart.Lots.Single(x => x.ListingId == test.Id);
        Assert.Null(hidden.Title);
        Assert.Null(hidden.Price);
        Assert.NotNull(hidden.UnavailableReason);
        var checkout = await Checkout(buyer, Review(cart with { Lots = cart.Lots.Where(x => x.ListingId == second).ToArray() }));
        Assert.Equal(second, Assert.Single(checkout.Orders).ListingId);
        var remaining = await Read(buyer);
        Assert.Equal(test.Id, Assert.Single(remaining.Lots).ListingId);
        (await buyer.DeleteAsync($"{Path}/lots/{test.Id}?revision={remaining.Revision}")).EnsureSuccessStatusCode();
        Assert.Empty((await Read(buyer)).Lots);
    }

    [PostgresFact]
    public async Task Concurrent_buy_now_and_cart_never_partially_reserve_the_cart()
    {
        await using var test = await FileCase.CreateAsync();
        using var admin = await FixedPriceTests.Client(test, FaunexRoles.TenantAdmin);
        using var buyer = await FixedPriceTests.Client(test, FaunexRoles.Buyer);
        using var rival = await FixedPriceTests.Client(test, FaunexRoles.Buyer);
        await FixedPriceTests.MakeFixed(test);
        await FixedPriceTests.Configure(test, admin);
        var second = await AddLot(test, 750m);
        await Add(buyer, test.Id);
        var cart = await Add(buyer, second);
        var reviewed = Review(cart);
        var shared = reviewed.Lots.Single(x => x.ListingId == test.Id);
        var responses = await Task.WhenAll(buyer.PostAsJsonAsync(Path + "/checkout", reviewed),
            rival.PostAsJsonAsync($"/api/fixed-price/lots/{test.Id}/buy", new FixedPricePurchaseRequest(shared.ListingRevision, shared.ExpectedPrice, shared.CurrencyCode)));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var orders = (await buyer.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders"))!.Items;
        Assert.Equal(responses[0].IsSuccessStatusCode ? 2 : 0, orders.Count);
        Assert.Equal(responses[0].IsSuccessStatusCode ? 0 : 2, (await Read(buyer)).Lots.Count);
    }

    private static CartCheckoutRequest Review(BuyerCartDto cart) =>
        new(cart.Revision, cart.Lots.Select(x => new CartCheckoutLot(x.ListingId, x.ListingRevision!.Value, x.Price!.Value, x.CurrencyCode!)).ToArray());
    private static async Task<BuyerCartDto> Read(HttpClient buyer)
    {
        using var response = await buyer.GetAsync(Path);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<BuyerCartDto>())!;
    }
    private static async Task<BuyerCartDto> Add(HttpClient buyer, Guid id)
    {
        using var response = await buyer.PutAsJsonAsync($"{Path}/lots/{id}", new { });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BuyerCartDto>())!;
    }
    private static async Task<CartCheckoutResult> Checkout(HttpClient buyer, CartCheckoutRequest request)
    {
        using var response = await buyer.PostAsJsonAsync(Path + "/checkout", request);
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<CartCheckoutResult>())!;
    }
    private static async Task<Guid> AddLot(FileCase test, decimal price)
    {
        using var scope = test.App.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seller = await db.Listings.IgnoreQueryFilters().Where(x => x.Id == test.Id).Select(x => x.SellerId).SingleAsync();
        var lot = new Listing { TenantId = test.Tenant, SellerId = seller, Title = "Second cart lot", IsActive = true,
            Quantity = 2, CurrencyCode = "ZAR", StartingPrice = price, BuyNowPrice = price };
        lot.Compliance = new ListingCompliance { ListingId = lot.Id, TenantId = test.Tenant, Status = ListingComplianceStatus.Approved };
        db.Listings.Add(lot);
        await db.SaveChangesAsync();
        return lot.Id;
    }
}
