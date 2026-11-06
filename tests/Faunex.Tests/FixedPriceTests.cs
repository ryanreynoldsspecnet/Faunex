using System.Net;
using System.Net.Http.Json;
using System.Text;
using Faunex.Api.Payments;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Faunex.Tests;

public sealed class FixedPriceTests
{
    private const string Root = "/api/fixed-price";

    [Fact]
    public async Task Buy_now_reserves_whole_lot_and_checkout_uses_recorded_delivery_and_extras()
    {
        await using var test = await FileCase.CreateAsync();
        using var admin = await Client(test, FaunexRoles.TenantAdmin);
        using var buyer = await Client(test, FaunexRoles.Buyer);
        using var rival = await Client(test, FaunexRoles.Buyer);
        var path = $"{Root}/lots/{test.Id}";
        await MakeFixed(test);
        var unconfigured = (await test.App.Client.GetFromJsonAsync<FixedPriceAvailabilityDto>(path))!;
        Assert.False(unconfigured.Available);
        await Configure(test, admin);
        var lot = (await test.App.Client.GetFromJsonAsync<FixedPriceAvailabilityDto>(path))!;
        Assert.True(lot.Available);
        var request = new FixedPricePurchaseRequest(lot.Revision, lot.Price, lot.CurrencyCode);
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.PostAsJsonAsync(path + "/buy", request with { ExpectedPrice = 0.01m })).StatusCode);
        var orderId = (await Send<FixedPriceOrderReference>(buyer, path + "/buy", request)).OrderId;
        Assert.Equal(orderId, (await Send<FixedPriceOrderReference>(buyer, path + "/buy", request)).OrderId);
        Assert.Equal(HttpStatusCode.Conflict, (await rival.PostAsJsonAsync(path + "/buy", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await rival.GetAsync($"/api/orders/{orderId}")).StatusCode);
        Assert.False((await test.App.Client.GetFromJsonAsync<FixedPriceAvailabilityDto>(path))!.Available);
        var order = (await buyer.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{orderId}"))!;
        Assert.Null(order.AuctionId);
        Assert.Equal(3, order.Quantity);
        Assert.Equal(500m, order.Amount);
        Assert.True(order.CanRelease);
        var terms = (await buyer.GetFromJsonAsync<ChargeTermsDto>($"/api/orders/{orderId}/pricing/options"))!;
        var selection = new PriceSelectionRequest(terms.Revision, terms.Options.Single(x => x.Kind == "Delivery").Id,
            [terms.Options.Single(x => x.Kind == "OptionalExtra").Id]);
        var quote = await Send<OrderQuoteDto>(buyer, $"/api/orders/{orderId}/pricing/finalise", new AcceptPriceRequest(selection, 575m));
        Assert.Equal(575m, quote.Pricing.Total);
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.PostAsJsonAsync($"{Root}/orders/{orderId}/release", new { })).StatusCode);
        var checkout = await Send<PayFastCheckoutDto>(buyer, $"/api/payments/payfast/orders/{orderId}/checkout", new { });
        Assert.Equal(575m, checkout.Amount);
        Assert.Equal(PayFastProtocol.CheckoutUrl, checkout.ActionUrl);
        var fields = new List<CheckoutFieldDto>
        {
            new("m_payment_id", checkout.AttemptId.ToString("N")), new("pf_payment_id", "98765"),
            new("payment_status", "COMPLETE"), new("item_name", "Faunex order " + orderId.ToString("N")),
            new("amount_gross", "575.00"), new("merchant_id", checkout.MerchantId)
        };
        var canonical = PayFastProtocol.Canonical(fields);
        using var notification = new HttpRequestMessage(HttpMethod.Post, "/api/payments/payfast/notify")
        {
            Content = new StringContent(canonical + "&signature=" + PayFastProtocol.Signature(canonical, PayFastCase.Passphrase),
                Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        notification.Headers.Add(PayFastRuntime.IngressHeader, PayFastCase.Token);
        notification.Headers.Add(PayFastRuntime.SourceHeader, "197.97.145.145");
        (await test.App.Client.SendAsync(notification)).EnsureSuccessStatusCode();
        Assert.Equal("SandboxPaymentComplete", (await buyer.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{orderId}"))!.Status);
        using var scope = test.App.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Auctions.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.Bids.IgnoreQueryFilters().ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepting_offer_or_counteroffer_creates_one_order_at_agreed_amount(bool counter)
    {
        await using var test = await FileCase.CreateAsync();
        var adminUser = await test.App.AddUserAsync(test.Tenant, FaunexRoles.TenantAdmin);
        using var admin = test.App.AuthenticatedClient(await test.App.TokenAsync(adminUser));
        var buyerUser = await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer);
        using var buyer = test.App.AuthenticatedClient(await test.App.TokenAsync(buyerUser));
        using var other = await Client(test, FaunexRoles.Buyer);
        using var unrelatedSeller = await Client(test, FaunexRoles.Seller);
        await MakeFixed(test);
        await Configure(test, admin);
        var lot = (await test.App.Client.GetFromJsonAsync<FixedPriceAvailabilityDto>($"{Root}/lots/{test.Id}"))!;
        var offer = await Send<FixedPriceOfferDto>(buyer, $"{Root}/lots/{test.Id}/offers", new FixedPriceOfferRequest(lot.Revision, 350m, "ZAR"));
        var mail = test.App.Server.Services.GetRequiredService<TestEmailSender>();
        Assert.Equal(adminUser.Email, Assert.Single(mail.Notices).To);
        Assert.Empty((await other.GetFromJsonAsync<PagedResult<FixedPriceOfferDto>>(Root + "/offers"))!.Items);
        Assert.Empty((await unrelatedSeller.GetFromJsonAsync<PagedResult<FixedPriceOfferDto>>(Root + "/incoming"))!.Items);
        var decisionPath = $"{Root}/incoming/{offer.Id}/decision";
        var decision = new FixedPriceOfferDecision(offer.Revision, counter ? "counter" : "accept", counter ? 425m : null);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelatedSeller.PostAsJsonAsync(decisionPath, decision)).StatusCode);
        (await admin.PostAsJsonAsync(decisionPath, decision)).EnsureSuccessStatusCode();
        Assert.Equal(buyerUser.Email, mail.Notices.Last().To);
        var row = Assert.Single((await buyer.GetFromJsonAsync<PagedResult<FixedPriceOfferDto>>(Root + "/offers"))!.Items);
        if (counter)
        {
            Assert.Equal("Countered", row.Status);
            Assert.Equal(425m, row.CounterAmount);
            Assert.Null(row.OrderId);
            Assert.True((await test.App.Client.GetFromJsonAsync<FixedPriceAvailabilityDto>($"{Root}/lots/{test.Id}"))!.Available);
            var accept = new FixedPriceOfferDecision(row.Revision, "accept");
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"{Root}/offers/{row.Id}/decision", accept)).StatusCode);
            var first = await Send<FixedPriceOrderReference>(buyer, $"{Root}/offers/{row.Id}/decision", accept);
            Assert.Equal(first, await Send<FixedPriceOrderReference>(buyer, $"{Root}/offers/{row.Id}/decision", accept));
        }
        var order = Assert.Single((await buyer.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders"))!.Items);
        Assert.Equal(counter ? 425m : 350m, order.Amount);
        Assert.Equal(order.Id, Assert.Single((await buyer.GetFromJsonAsync<PagedResult<FixedPriceOfferDto>>(Root + "/offers"))!.Items).OrderId);
        Assert.Equal(HttpStatusCode.NoContent, (await buyer.PostAsJsonAsync($"{Root}/orders/{order.Id}/release", new { })).StatusCode);
        Assert.True((await test.App.Client.GetFromJsonAsync<FixedPriceAvailabilityDto>($"{Root}/lots/{test.Id}"))!.Available);
        Assert.Equal("Released", Assert.Single((await buyer.GetFromJsonAsync<PagedResult<FixedPriceOfferDto>>(Root + "/offers"))!.Items).Status);
    }

    [PostgresFact]
    public async Task Competing_buyers_can_create_only_one_active_reservation()
    {
        await using var test = await FileCase.CreateAsync();
        using var admin = await Client(test, FaunexRoles.TenantAdmin);
        using var buyer = await Client(test, FaunexRoles.Buyer);
        using var rival = await Client(test, FaunexRoles.Buyer);
        await MakeFixed(test);
        await Configure(test, admin);
        var path = $"{Root}/lots/{test.Id}";
        var lot = (await test.App.Client.GetFromJsonAsync<FixedPriceAvailabilityDto>(path))!;
        var request = new FixedPricePurchaseRequest(lot.Revision, lot.Price, lot.CurrencyCode);
        var responses = await Task.WhenAll(buyer.PostAsJsonAsync(path + "/buy", request), rival.PostAsJsonAsync(path + "/buy", request));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [PostgresFact]
    public async Task Finalising_price_and_releasing_a_reservation_cannot_both_succeed()
    {
        await using var test = await FileCase.CreateAsync();
        using var admin = await Client(test, FaunexRoles.TenantAdmin);
        using var buyer = await Client(test, FaunexRoles.Buyer);
        await MakeFixed(test);
        await Configure(test, admin);
        var path = $"{Root}/lots/{test.Id}";
        var lot = (await test.App.Client.GetFromJsonAsync<FixedPriceAvailabilityDto>(path))!;
        var id = (await Send<FixedPriceOrderReference>(buyer, path + "/buy",
            new FixedPricePurchaseRequest(lot.Revision, lot.Price, lot.CurrencyCode))).OrderId;
        var terms = (await buyer.GetFromJsonAsync<ChargeTermsDto>($"/api/orders/{id}/pricing/options"))!;
        var selection = new PriceSelectionRequest(terms.Revision, terms.Options.Single(x => x.Kind == "Delivery").Id, []);
        var results = await Task.WhenAll(buyer.PostAsJsonAsync($"/api/orders/{id}/pricing/finalise", new AcceptPriceRequest(selection, 550m)),
            admin.PostAsJsonAsync($"{Root}/orders/{id}/release", new { }));
        Assert.Single(results, x => x.IsSuccessStatusCode);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var order = (await buyer.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{id}"))!;
        Assert.NotEqual(order.Released, order.Quote is not null);
    }

    private static async Task MakeFixed(FileCase test)
    {
        await test.SetState(ListingComplianceStatus.Approved, true);
        using var scope = test.App.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lot = await db.Listings.IgnoreQueryFilters().SingleAsync(x => x.Id == test.Id);
        lot.StartingPrice = 500m; lot.BuyNowPrice = 500m; lot.Quantity = 3;
        await db.SaveChangesAsync();
    }
    private static async Task Configure(FileCase test, HttpClient admin)
    {
        var options = test.App.Server.Services.GetRequiredService<IOptions<PayFastOptions>>().Value;
        options.SandboxEnabled = true; options.IngressToken = PayFastCase.Token;
        (await admin.PutAsJsonAsync("/api/tenant/charges", new ChargeCatalogDto(Guid.Empty, true, "ZAR", "Test pricing",
            [new(Guid.Empty, "Transport", "Delivery", 50m), new(Guid.Empty, "Crate", "OptionalExtra", 25m)]))).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync("/api/tenant/payments/payfast",
            new SavePayFastSettingsRequest(Guid.Empty, "10000100", "46f0cd694581a", PayFastCase.Passphrase))).EnsureSuccessStatusCode();
    }
    private static async Task<HttpClient> Client(FileCase test, string role) =>
        test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(test.Tenant, role)));
    private static async Task<T> Send<T>(HttpClient client, string path, object request)
    {
        using var response = await client.PostAsJsonAsync(path, request);
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
