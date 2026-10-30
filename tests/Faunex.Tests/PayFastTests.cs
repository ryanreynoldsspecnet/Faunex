using System.Globalization;
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

public sealed class PayFastTests
{
    [Fact]
    public async Task Ordered_form_parser_preserves_blank_values_and_unicode()
    {
        List<CheckoutFieldDto> fields = [new("custom_str1", ""), new("name_first", "Ren\u00e9e & Co"), new("signature", new string('a', 32))];
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PayFastProtocol.Canonical(fields)));
        var read = await PayFastProtocol.ReadNotificationAsync(stream, CancellationToken.None);
        Assert.Equal(fields, read);
    }
    [Fact]
    public void Custom_signature_vectors_encoding_and_actual_source_ranges_are_explicit()
    {
        Assert.Equal("Ren%C3%A9e+%26+Co", PayFastProtocol.Encode("Ren\u00e9e & Co"));
        Assert.Equal("%7E%21%2A%28%29%27%2F%2B", PayFastProtocol.Encode("~!*()'/+"));
        Assert.Equal("0050620e20f1539f9fb5d9c4847d30fc", PayFastProtocol.Signature("merchant_id=10000100&merchant_key=46f0cd694581a&amount=100.00&item_name=Test+Product", PayFastCase.Passphrase));
        Assert.Equal("3575659e1d15952c1207869d19e71c0f", PayFastProtocol.Signature("name_first=Ren%C3%A9e+%26+Co&custom_str1=%7E%21%2A%28%29%27%2F%2B", "Phrase/1"));
        var verifier = new PayFastVerifier(new HttpClient());
        foreach (var address in new[] { "197.97.145.144", "197.97.145.159", "41.74.179.223", "102.216.36.15", "102.216.36.143", "144.126.193.139", "::ffff:197.97.145.145" })
            Assert.True(verifier.IsSourceAllowed(IPAddress.Parse(address)));
        foreach (var address in new[] { "127.0.0.1", "197.97.145.160", "102.216.36.16", "144.126.193.140", "::1" })
            Assert.False(verifier.IsSourceAllowed(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("VALID", true)] [InlineData("INVALID", false)] [InlineData("invalid response", false)]
    public async Task Server_validation_uses_fixed_sandbox_endpoint_and_encoded_data(string reply, bool valid)
    {
        var handler = new ValidationHandler(reply);
        var verifier = new PayFastVerifier(new HttpClient(handler));
        Assert.Equal(valid, await verifier.ValidateAsync("name_first=A+B&amount_gross=100.00", CancellationToken.None));
        Assert.Equal(PayFastProtocol.ValidationUrl, handler.Url); Assert.Equal("name_first=A+B&amount_gross=100.00", handler.Body);
    }

    [Fact]
    public async Task Checkout_uses_accepted_total_merchant_snapshot_fixed_sandbox_and_one_pending_reference()
    {
        await using var test = await PayFastCase.CreateAsync();
        var checkout = await test.Prepare(); var repeated = await test.Prepare(); Assert.Equal(checkout.AttemptId, repeated.AttemptId);
        Assert.Equal(100m, checkout.Amount); Assert.Equal("ZAR", checkout.CurrencyCode); Assert.Equal(PayFastProtocol.CheckoutUrl, checkout.ActionUrl);
        Assert.Equal("10000100", checkout.MerchantId); Assert.DoesNotContain(checkout.Fields, x => x.Name == "passphrase");
        Assert.Equal(new[] { "merchant_id", "merchant_key", "return_url", "cancel_url", "notify_url", "m_payment_id", "amount", "item_name", "signature" }, checkout.Fields.Select(x => x.Name));
        Assert.Equal("100.00", checkout.Fields.Single(x => x.Name == "amount").Value);
        Assert.Equal(PayFastRuntime.PublicNotifyUrl, checkout.Fields.Single(x => x.Name == "notify_url").Value);
        Assert.StartsWith("https://" + AccountApiFixture.TenantHost(test.Auction.Files.Tenant), checkout.Fields.Single(x => x.Name == "return_url").Value);
        using var scope = test.App.Server.Services.CreateScope(); var attempt = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PayFastAttempts.IgnoreQueryFilters().SingleAsync();
        Assert.DoesNotContain(PayFastCase.Passphrase, attempt.ProtectedCredentials);
        var orderJson = await test.Buyer.GetStringAsync($"/api/orders/{test.Order.Id}");
        Assert.DoesNotContain("46f0cd694581a", orderJson); Assert.DoesNotContain(PayFastCase.Passphrase, orderJson);
        Assert.Equal("Pending", (await test.Payment()).Status);
    }

    [Theory]
    [InlineData("flag")][InlineData("ingress")][InlineData("credentials")][InlineData("quote")][InlineData("currency")]
    public async Task Missing_readiness_or_incompatible_order_fails_closed(string missing)
    {
        await using var test = await PayFastCase.CreateAsync(withQuote: missing != "quote", currency: missing == "currency" ? "USD" : "ZAR");
        if (missing == "flag") test.Options.SandboxEnabled = false;
        if (missing == "ingress") test.Options.IngressToken = "";
        if (missing == "credentials")
        {
            var row = (await test.Auction.Admin.GetFromJsonAsync<PayFastSettingsDto>("/api/tenant/payments/payfast"))!;
            Assert.True((await test.Auction.Admin.DeleteAsync($"/api/tenant/payments/payfast?revision={row.Revision}")).IsSuccessStatusCode);
        }
        var response = await test.Buyer.PostAsJsonAsync(test.CheckoutPath, new { Amount = 0.01m, MerchantId = "99999999" });
        Assert.Equal(missing == "quote" ? HttpStatusCode.Conflict : missing == "currency" ? HttpStatusCode.BadRequest : HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var scope = test.App.Server.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PayFastAttempts.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Checkout_payment_history_and_return_queries_do_not_bypass_owner_or_tenant_rules()
    {
        await using var test = await PayFastCase.CreateAsync(); await test.Prepare();
        using var loser = await test.Auction.BuyerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await loser.PostAsJsonAsync(test.CheckoutPath, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await loser.GetAsync($"/api/orders/{test.Order.Id}/payment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await test.Auction.Admin.PostAsJsonAsync(test.CheckoutPath, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await test.Auction.Admin.GetAsync($"/api/orders/{test.Order.Id}/payment")).StatusCode);
        var foreign = await test.App.AddTenantAsync(); using var other = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(foreign, FaunexRoles.Buyer)));
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(test.CheckoutPath, new { })).StatusCode);
        var returned = (await test.Buyer.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{test.Order.Id}?payment=return&paid=true"))!;
        Assert.Equal("Pending", returned.Payment!.Status); Assert.Null(returned.Payment.ConfirmedAt);
    }

    [Fact]
    public async Task Verified_complete_is_durable_idempotent_and_noncompletion_cannot_undo_it()
    {
        await using var test = await PayFastCase.CreateAsync(); var checkout = await test.Prepare();
        var body = test.Body(checkout); var receipt = await test.Notify(body);
        Assert.True(receipt.StatusCode == HttpStatusCode.OK, $"{receipt.StatusCode}: {await receipt.Content.ReadAsStringAsync()}");
        var first = await test.Payment(); Assert.Equal("SandboxComplete", first.Status); Assert.NotNull(first.ConfirmedAt); Assert.Single(first.Events);
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(body)).StatusCode); Assert.Equal(1, test.Verifier.Calls);
        Assert.Equal(first.ConfirmedAt, (await test.Payment()).ConfirmedAt); Assert.Single((await test.Payment()).Events);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Buyer.PostAsJsonAsync(test.CheckoutPath, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout, status: "CANCELLED"))).StatusCode);
        Assert.Equal("SandboxComplete", (await test.Payment()).Status);
        var order = (await test.Buyer.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{test.Order.Id}"))!;
        Assert.Equal("SandboxPaymentComplete", order.Status); Assert.False(order.CheckoutAvailable);
    }

    [Theory]
    [InlineData("FAILED")][InlineData("CANCELLED")][InlineData("PENDING")]
    public async Task Noncompletion_stays_unpaid_and_checkout_retry_reuses_the_same_intent(string status)
    {
        await using var test = await PayFastCase.CreateAsync(); var checkout = await test.Prepare();
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout, status: status))).StatusCode);
        Assert.Equal("Pending", (await test.Payment()).Status); Assert.Equal(checkout.AttemptId, (await test.Prepare()).AttemptId);
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout))).StatusCode); Assert.Equal("SandboxComplete", (await test.Payment()).Status);
    }

    [Theory]
    [InlineData("merchant")][InlineData("amount")][InlineData("signature")][InlineData("reference")][InlineData("currency")]
    [InlineData("duplicate-field")][InlineData("after-signature")][InlineData("missing-signature")][InlineData("oversized")]
    public async Task Invalid_notifications_never_change_state_or_reach_provider_validation(string invalid)
    {
        await using var test = await PayFastCase.CreateAsync(); var checkout = await test.Prepare(); var fields = test.Fields(checkout);
        if (invalid is "merchant" or "amount" or "reference")
        {
            var key = invalid == "merchant" ? "merchant_id" : invalid == "amount" ? "amount_gross" : "m_payment_id";
            fields = fields.Select(x => x.Name == key ? x with { Value = invalid == "merchant" ? "99999999" : invalid == "amount" ? "99.99" : Guid.NewGuid().ToString("N") } : x).ToList();
        }
        if (invalid == "currency") fields.Add(new("currency", "USD"));
        var body = test.Sign(fields, invalid == "signature" ? "wrong" : PayFastCase.Passphrase);
        body = invalid switch { "duplicate-field" => "merchant_id=10000100&" + body, "after-signature" => body + "&amount_gross=0.01", "missing-signature" => PayFastProtocol.Canonical(fields), "oversized" => new string('x', 17000), _ => body };
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Notify(body)).StatusCode);
        Assert.Equal("Pending", (await test.Payment()).Status); Assert.Empty((await test.Payment()).Events); Assert.Equal(0, test.Verifier.Calls);
    }

    [Fact]
    public async Task Forged_ingress_bad_source_invalid_provider_response_and_outage_fail_closed()
    {
        await using var test = await PayFastCase.CreateAsync(); var checkout = await test.Prepare(); var body = test.Body(checkout);
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Notify(body, token: "forged")).StatusCode);
        test.Verifier.SourceAllowed = false; Assert.Equal(HttpStatusCode.BadRequest, (await test.Notify(body)).StatusCode); test.Verifier.SourceAllowed = true;
        test.Verifier.Result = false; Assert.Equal(HttpStatusCode.BadRequest, (await test.Notify(body)).StatusCode);
        test.Verifier.Handler = _ => throw new PaymentUnavailableException("Test provider unavailable"); Assert.Equal(HttpStatusCode.ServiceUnavailable, (await test.Notify(body)).StatusCode);
        Assert.Empty((await test.Payment()).Events); Assert.Equal("Pending", (await test.Payment()).Status);
        test.Verifier.Handler = null; test.Verifier.Result = true;
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(body)).StatusCode); Assert.Equal("SandboxComplete", (await test.Payment()).Status);
    }

    [Fact]
    public async Task Late_confirmation_uses_captured_credentials_even_after_removal_disable_and_tenant_deactivation()
    {
        await using var test = await PayFastCase.CreateAsync(); var checkout = await test.Prepare();
        var settings = (await test.Auction.Admin.GetFromJsonAsync<PayFastSettingsDto>("/api/tenant/payments/payfast"))!;
        var changed = await test.Auction.Admin.PutAsJsonAsync("/api/tenant/payments/payfast", new SavePayFastSettingsRequest(settings.Revision, "10000101", "123456789abcd", "New-Phrase"));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Buyer.PostAsJsonAsync(test.CheckoutPath, new { })).StatusCode);
        settings = (await changed.Content.ReadFromJsonAsync<PayFastSettingsDto>())!;
        Assert.True((await test.Auction.Admin.DeleteAsync($"/api/tenant/payments/payfast?revision={settings.Revision}")).IsSuccessStatusCode);
        test.Options.SandboxEnabled = false;
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); (await db.Tenants.SingleAsync()).IsActive = false; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout))).StatusCode);
        using var final = test.App.Server.Services.CreateScope();
        Assert.Equal("SandboxComplete", (await final.ServiceProvider.GetRequiredService<ApplicationDbContext>().PayFastAttempts.IgnoreQueryFilters().SingleAsync()).Status);
    }

    [Fact]
    public async Task Different_successful_provider_transaction_is_flagged_for_review_and_snapshot_changes_are_rejected()
    {
        await using var test = await PayFastCase.CreateAsync(); var checkout = await test.Prepare();
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout, provider: "99999"))).StatusCode);
        var summary = await test.Payment(); Assert.Equal("ReviewRequired", summary.Status); Assert.Equal("12345", summary.ProviderPaymentId); Assert.Equal(2, summary.Events.Count);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Buyer.PostAsJsonAsync(test.CheckoutPath, new { })).StatusCode);
        using var scope = test.App.Server.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempt = await db.PayFastAttempts.IgnoreQueryFilters().SingleAsync(); attempt.Amount = 1m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var notification = await db.PayFastNotifications.IgnoreQueryFilters().FirstAsync(); db.PayFastNotifications.Remove(notification);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Concurrent_checkout_and_duplicate_notifications_keep_one_intent_one_verified_event()
    {
        await using var test = await PayFastCase.CreateAsync();
        var starts = await Task.WhenAll(test.Buyer.PostAsJsonAsync(test.CheckoutPath, new { }), test.Buyer.PostAsJsonAsync(test.CheckoutPath, new { }));
        Assert.Contains(starts, x => x.StatusCode == HttpStatusCode.OK); Assert.All(starts, x => Assert.True(x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        var checkout = await test.Prepare(); var body = test.Body(checkout);
        var receipts = await Task.WhenAll(test.Notify(body), test.Notify(body));
        Assert.Contains(receipts, x => x.StatusCode == HttpStatusCode.OK); Assert.All(receipts, x => Assert.True(x.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable));
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(body)).StatusCode);
        Assert.Single((await test.Payment()).Events); Assert.Equal("SandboxComplete", (await test.Payment()).Status);
        using var scope = test.App.Server.Services.CreateScope(); Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PayFastAttempts.IgnoreQueryFilters().ToListAsync());
        foreach (var response in starts.Concat(receipts)) response.Dispose();
    }
}

public sealed class TestPayFastVerifier : IPayFastVerifier
{
    public bool SourceAllowed { get; set; } = true;
    public bool Result { get; set; } = true;
    public int Calls;
    public Func<string, Task<bool>>? Handler { get; set; }
    public bool IsSourceAllowed(IPAddress address) => SourceAllowed;
    public Task<bool> ValidateAsync(string canonical, CancellationToken ct) { Interlocked.Increment(ref Calls); return Handler?.Invoke(canonical) ?? Task.FromResult(Result); }
}

internal sealed class ValidationHandler(string reply) : HttpMessageHandler
{
    public string? Url { get; private set; }
    public string? Body { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    { Url = request.RequestUri!.AbsoluteUri; Body = await request.Content!.ReadAsStringAsync(ct); return new(HttpStatusCode.OK) { Content = new StringContent(reply) }; }
}

internal sealed class PayFastCase : IAsyncDisposable
{
    public const string Passphrase = "Sandbox_Phrase-1";
    public const string Token = "FAUNEX_TEST_ONLY_CALLBACK_INGRESS_TOKEN_123456789";
    public AuctionCase Auction { get; private init; } = null!;
    public HttpClient Buyer { get; private init; } = null!;
    public AuctionOrderDto Order { get; private init; } = null!;
    public AccountApiFixture App => Auction.Files.App;
    public PayFastOptions Options => App.Server.Services.GetRequiredService<IOptions<PayFastOptions>>().Value;
    public TestPayFastVerifier Verifier => App.Server.Services.GetRequiredService<TestPayFastVerifier>();
    public string CheckoutPath => $"/api/payments/payfast/orders/{Order.Id}/checkout";
    public async Task<PayFastCheckoutDto> Prepare() { var response = await Buyer.PostAsJsonAsync(CheckoutPath, new { Amount = 0.01m }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<PayFastCheckoutDto>())!; }
    public async Task<PayFastSummaryDto> Payment() => (await Buyer.GetFromJsonAsync<PayFastSummaryDto>($"/api/orders/{Order.Id}/payment"))!;
    public List<CheckoutFieldDto> Fields(PayFastCheckoutDto checkout, string provider = "12345", string status = "COMPLETE") =>
        [new("m_payment_id", checkout.AttemptId.ToString("N")), new("pf_payment_id", provider), new("payment_status", status),
         new("item_name", "Faunex order " + Order.Id.ToString("N")), new("amount_gross", checkout.Amount.ToString("0.00", CultureInfo.InvariantCulture)),
         new("amount_fee", "-2.00"), new("custom_str1", ""), new("name_first", "Ren\u00e9e & Co"), new("merchant_id", checkout.MerchantId)];
    public string Sign(IReadOnlyList<CheckoutFieldDto> fields, string passphrase) { var canonical = PayFastProtocol.Canonical(fields); return canonical + "&signature=" + PayFastProtocol.Signature(canonical, passphrase); }
    public string Body(PayFastCheckoutDto checkout, string provider = "12345", string status = "COMPLETE") => Sign(Fields(checkout, provider, status), Passphrase);
    public async Task<HttpResponseMessage> Notify(string body, string token = Token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/payfast/notify") { Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded") };
        request.Headers.Add(PayFastRuntime.IngressHeader, token); request.Headers.Add(PayFastRuntime.SourceHeader, "197.97.145.145");
        return await App.Client.SendAsync(request);
    }
    public static async Task<PayFastCase> CreateAsync(bool withQuote = true, string currency = "ZAR")
    {
        var auction = await AuctionCase.CreateAsync();
        var options = auction.Files.App.Server.Services.GetRequiredService<IOptions<PayFastOptions>>().Value; options.SandboxEnabled = true; options.IngressToken = Token;
        if (currency != "ZAR")
        {
            using (var scope = auction.Files.App.Server.Services.CreateScope())
            { var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); (await db.Listings.IgnoreQueryFilters().SingleAsync()).CurrencyCode = currency; await db.SaveChangesAsync(); }
            Assert.True((await auction.Admin.PutAsJsonAsync($"/api/auctions/{auction.AuctionId}", await auction.Snapshot())).IsSuccessStatusCode);
        }
        var catalog = new ChargeCatalogDto(Guid.Empty, true, currency, "Test-only pricing terms.", [new(Guid.Empty, "Collection", "Delivery", 0)]);
        Assert.True((await auction.Admin.PutAsJsonAsync("/api/tenant/charges", catalog)).IsSuccessStatusCode);
        Assert.True((await auction.Admin.PutAsJsonAsync("/api/tenant/payments/payfast", new SavePayFastSettingsRequest(Guid.Empty, "10000100", "46f0cd694581a", Passphrase))).IsSuccessStatusCode);
        await auction.Publish(); var buyer = await auction.BuyerAsync(); auction.Clock.Set(auction.Starts); Assert.True((await auction.Bid(buyer, 100m)).IsSuccessStatusCode); auction.Clock.Set(auction.Ends);
        Assert.True((await auction.Admin.PostAsJsonAsync($"/api/auctions/{auction.AuctionId}/close", new { (await auction.Snapshot()).Revision })).IsSuccessStatusCode);
        var order = Assert.Single((await buyer.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders"))!.Items);
        if (withQuote)
        {
            var terms = (await buyer.GetFromJsonAsync<ChargeTermsDto>($"/api/orders/{order.Id}/pricing/options"))!;
            Assert.True((await buyer.PostAsJsonAsync($"/api/orders/{order.Id}/pricing/finalise", new AcceptPriceRequest(new(terms.Revision, terms.Options[0].Id, []), 100m))).IsSuccessStatusCode);
        }
        return new() { Auction = auction, Buyer = buyer, Order = order };
    }
    public async ValueTask DisposeAsync() { Buyer.Dispose(); await Auction.DisposeAsync(); }
}
