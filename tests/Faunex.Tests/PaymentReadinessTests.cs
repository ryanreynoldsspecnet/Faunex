using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Faunex.Api.Payments;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class PaymentReadinessTests
{
    private const string SettingsPath = "/api/tenant/payments/payfast";
    private static SavePayFastSettingsRequest Credentials(Guid revision) => new(revision, "10000100", "46f0cd694581a", "Sandbox_Phrase-1");

    [Fact]
    public async Task Settings_are_write_only_protected_tenant_bound_revision_checked_and_removable()
    {
        await using var app = await AccountApiFixture.CreateAsync(); var tenant = await app.AddTenantAsync();
        var user = await app.AddUserAsync(tenant, FaunexRoles.TenantAdmin);
        using var admin = app.AuthenticatedClient(await app.TokenAsync(user));
        var initial = (await admin.GetFromJsonAsync<PayFastSettingsDto>(SettingsPath))!;
        Assert.False(initial.HasCredentials); Assert.Equal(Guid.Empty, initial.Revision);
        var request = Credentials(initial.Revision);
        var response = await admin.PutAsJsonAsync(SettingsPath, request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = (await response.Content.ReadFromJsonAsync<PayFastSettingsDto>())!;
        Assert.True(row.HasCredentials); Assert.False(row.CheckoutAvailable); Assert.Equal("Sandbox", row.Environment);
        Assert.DoesNotContain(request.MerchantKey!, await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(request.Passphrase!, await response.Content.ReadAsStringAsync());
        string encrypted;
        using (var scope = app.Server.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TenantPaymentSettings.IgnoreQueryFilters().SingleAsync();
            Assert.Equal(user.Id, stored.UpdatedBy); encrypted = stored.ProtectedCredentials;
            Assert.DoesNotContain(request.MerchantKey!, encrypted); Assert.DoesNotContain(request.Passphrase!, encrypted);
            var protector = scope.ServiceProvider.GetRequiredService<IPaymentSecretProtector>();
            Assert.Contains(request.MerchantKey!, protector.Unprotect(tenant, encrypted));
            Assert.Throws<CryptographicException>(() => protector.Unprotect(Guid.NewGuid(), encrypted));
        }
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(SettingsPath, request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(SettingsPath, new SavePayFastSettingsRequest(row.Revision, "10000101", "", ""))).StatusCode);
        var kept = await admin.PutAsJsonAsync(SettingsPath, new SavePayFastSettingsRequest(row.Revision, row.MerchantId, "", ""));
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode); var updated = (await kept.Content.ReadFromJsonAsync<PayFastSettingsDto>())!;
        using (var scope = app.Server.Services.CreateScope())
            Assert.Equal(encrypted, (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TenantPaymentSettings.IgnoreQueryFilters().SingleAsync()).ProtectedCredentials);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"{SettingsPath}?revision={row.Revision}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{SettingsPath}?revision={updated.Revision}")).StatusCode);
        Assert.False((await admin.GetFromJsonAsync<PayFastSettingsDto>(SettingsPath))!.HasCredentials);
    }

    [Theory]
    [InlineData("Live", "10000100", "46f0cd694581a", "phrase")]
    [InlineData("Sandbox", "invalid", "46f0cd694581a", "phrase")]
    [InlineData("Sandbox", "10000100", "bad", "phrase")]
    [InlineData("Sandbox", "10000100", "46f0cd694581a", "")]
    [InlineData("Sandbox", "10000100", "46f0cd694581a", "invalid&phrase")]
    public async Task Invalid_settings_and_live_mode_are_rejected(string mode, string id, string key, string phrase)
    {
        await using var app = await AccountApiFixture.CreateAsync(); var tenant = await app.AddTenantAsync();
        using var admin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(tenant, FaunexRoles.TenantAdmin)));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(SettingsPath, new SavePayFastSettingsRequest(Guid.Empty, id, key, phrase, mode))).StatusCode);
        Assert.False((await admin.GetFromJsonAsync<PayFastSettingsDto>(SettingsPath))!.HasCredentials);
    }

    [Fact]
    public async Task Payment_settings_deny_buyers_sellers_platform_staff_and_wrong_hosts()
    {
        await using var app = await AccountApiFixture.CreateAsync(); var tenant = await app.AddTenantAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync(SettingsPath)).StatusCode);
        foreach (var role in new[] { FaunexRoles.Buyer, FaunexRoles.Seller, FaunexRoles.PlatformSuperAdmin })
        {
            using var denied = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(role.StartsWith("Platform") ? null : tenant, role)));
            Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(SettingsPath)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await denied.PutAsJsonAsync(SettingsPath, Credentials(Guid.Empty))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await denied.DeleteAsync($"{SettingsPath}?revision={Guid.Empty}")).StatusCode);
        }
        using var admin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(tenant, FaunexRoles.TenantAdmin)));
        Assert.True((await admin.PutAsJsonAsync(SettingsPath, Credentials(Guid.Empty))).IsSuccessStatusCode);
        var other = await app.AddTenantAsync();
        using var otherAdmin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(other, FaunexRoles.TenantAdmin)));
        Assert.False((await otherAdmin.GetFromJsonAsync<PayFastSettingsDto>(SettingsPath))!.HasCredentials);
        admin.DefaultRequestHeaders.Remove(Faunex.Application.Tenancy.MarketplaceHosts.OriginHeader);
        admin.DefaultRequestHeaders.Add(Faunex.Application.Tenancy.MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(other));
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(SettingsPath)).StatusCode);
    }

    [Theory]
    [InlineData(null, null, false)] [InlineData(150, 100, false)] [InlineData(150, 150, true)]
    public async Task Only_sold_results_create_one_snapshot_and_only_the_winner_can_read_it(int? reserve, int? amount, bool sold)
    {
        await using var test = await AuctionCase.CreateAsync(reserve); await test.Publish(); test.Clock.Set(test.Starts);
        using var winner = await test.BuyerAsync();
        if (amount.HasValue) Assert.True((await test.Bid(winner, amount.Value)).IsSuccessStatusCode);
        test.Clock.Set(test.Ends);
        var revision = (await test.Snapshot()).Revision;
        Assert.Equal(HttpStatusCode.NoContent, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { Revision = revision })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { Revision = revision })).StatusCode);
        var orders = (await winner.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders"))!;
        Assert.Equal(sold ? 1 : 0, orders.Total);
        if (!sold) { Assert.Equal(HttpStatusCode.NotFound, (await winner.PostAsJsonAsync($"/api/orders/from-auction/{test.AuctionId}", new { })).StatusCode); return; }
        var order = Assert.Single(orders.Items);
        Assert.Equal(amount!.Value, order.Amount); Assert.Equal("ZAR", order.CurrencyCode); Assert.Equal("AwaitingPayment", order.Status); Assert.False(order.CheckoutAvailable);
        var repeated = await winner.PostAsJsonAsync($"/api/orders/from-auction/{test.AuctionId}", new { Amount = 0.01m, BuyerId = Guid.NewGuid() });
        Assert.Equal(order, await repeated.Content.ReadFromJsonAsync<AuctionOrderDto>());
        Assert.Equal(order, await test.Admin.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{order.Id}"));
        using var loser = await test.BuyerAsync();
        Assert.Empty((await loser.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await loser.GetAsync($"/api/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await loser.PostAsJsonAsync($"/api/orders/from-auction/{test.AuctionId}", new { })).StatusCode);
        var foreign = await test.Files.App.AddTenantAsync();
        using var foreignAdmin = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(await test.Files.App.AddUserAsync(foreign, FaunexRoles.TenantAdmin)));
        Assert.Empty((await foreignAdmin.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await foreignAdmin.GetAsync($"/api/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await winner.GetAsync("/api/orders?search=" + new string('a', 201))).StatusCode);
        using var scope = test.Files.App.Server.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.AuctionOrders.IgnoreQueryFilters().SingleAsync(); stored.Amount = 1m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Existing_closed_result_can_recover_one_order_but_inconsistent_results_cannot()
    {
        await using var test = await AuctionCase.CreateAsync(); await test.Publish(); test.Clock.Set(test.Starts);
        using var winner = await test.BuyerAsync(); Assert.True((await test.Bid(winner, 100m)).IsSuccessStatusCode); test.Clock.Set(test.Ends);
        // Represent a result committed by Section 5 before orders existed.
        using (var scope = test.Files.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var auction = await db.Auctions.IgnoreQueryFilters().SingleAsync();
            auction.State = Faunex.Domain.Enums.AuctionState.Closed; auction.IsClosed = true; auction.ClosedAt = test.Ends;
            auction.Outcome = "Sold"; auction.WinnerId = auction.LeadingBidderId; auction.WinningBidId = auction.LeadingBidId; await db.SaveChangesAsync();
        }
        var response = await winner.PostAsJsonAsync($"/api/orders/from-auction/{test.AuctionId}", new { }); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<AuctionOrderDto>())!;
        Assert.Equal(order, await (await winner.PostAsJsonAsync($"/api/orders/from-auction/{test.AuctionId}", new { })).Content.ReadFromJsonAsync<AuctionOrderDto>());
        var bad = new Auction { RulesVersion = 1, Outcome = "Sold" };
        Assert.Throws<ListingEditConflictException>(() => WinnerOrderFactory.Create(bad, new Listing(), new Bid(), test.Ends));
    }

    [PostgresFact]
    public async Task Competing_settings_saves_keep_one_revision_and_credentials()
    {
        await using var app = await AccountApiFixture.CreateAsync(); var tenant = await app.AddTenantAsync();
        using var admin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(tenant, FaunexRoles.TenantAdmin)));
        var results = await Task.WhenAll(admin.PutAsJsonAsync(SettingsPath, Credentials(Guid.Empty)), admin.PutAsJsonAsync(SettingsPath, Credentials(Guid.Empty)));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in results) response.Dispose();
    }

    [Fact]
    public void Dedicated_key_ring_survives_provider_recreation_and_rejects_tampering()
    {
        var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "faunex-payment-test-" + Guid.NewGuid().ToString("N")));
        try
        {
            var tenant = Guid.NewGuid();
            var first = new PaymentSecretProtector(DataProtectionProvider.Create(directory, b => b.SetApplicationName("Faunex.Payments")));
            var encrypted = first.Protect(tenant, "test-only-secret");
            var second = new PaymentSecretProtector(DataProtectionProvider.Create(directory, b => b.SetApplicationName("Faunex.Payments")));
            Assert.Equal("test-only-secret", second.Unprotect(tenant, encrypted));
            Assert.Throws<CryptographicException>(() => second.Unprotect(tenant, encrypted[..^10] + "not-valid!"));
        }
        finally { if (directory.Exists) directory.Delete(true); }
    }
}
