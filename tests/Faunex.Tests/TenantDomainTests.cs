using System.Net;
using System.Net.Http.Json;
using Faunex.Api.Auth;
using Faunex.Api.Controllers.Platform;
using Faunex.Application.Auth;
using Faunex.Application.Tenancy;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class TenantDomainTests
{
    [Theory]
    [InlineData("Faunex.co.za", false)]
    [InlineData("www.faunex.co.za", false)]
    [InlineData("admin.faunex.co.za", false)]
    [InlineData("api.faunex.co.za", false)]
    [InlineData("stormbird.faunex.co.za", true)]
    [InlineData("auction.example.co.za", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("localhost", false)]
    [InlineData("auction.local", false)]
    [InlineData("auction.internal", false)]
    [InlineData("bad_host.example.com", false)]
    [InlineData("a.b.faunex.co.za", false)]
    public void Only_assignable_public_hosts_are_tenant_domains(string host, bool expected) =>
        Assert.Equal(expected, MarketplaceHosts.IsTenantDomain(host));

    [Theory]
    [InlineData("https://SHOP.example.co.za:443/path", "shop.example.co.za")]
    [InlineData("SHOP.example.co.za.", "shop.example.co.za")]
    [InlineData("https://user:pass@example.co.za", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("not a host", null)]
    public void Host_normalization_rejects_ambiguous_values(string raw, string? expected) =>
        Assert.Equal(expected, MarketplaceHosts.Normalize(raw));

    [Theory]
    [InlineData("https://example.co.za/logo.png", true)]
    [InlineData("/images/logo.png", true)]
    [InlineData("//example.co.za/logo.png", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("http://example.co.za/logo.png", false)]
    [InlineData("data:image/svg+xml,anything", false)]
    public void Branding_accepts_only_safe_logo_sources(string value, bool expected) =>
        Assert.Equal(expected, TenantBrandingRules.IsLogo(value));

    [Fact]
    public async Task Custom_domain_stays_closed_until_current_DNS_proof_then_disable_and_delete_close_it()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        using var admin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin)));
        var path = $"/api/platform/tenants/{tenant}/domains";
        var created = await admin.PostAsJsonAsync(path, new CreateTenantDomainRequest("auction.example.co.za"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var domain = (await created.Content.ReadFromJsonAsync<TenantDomainDto>())!;
        Assert.Null(domain.VerifiedAt);
        Assert.Equal("_faunex-verification.auction.example.co.za", domain.VerificationRecordName);
        Assert.Equal(71, domain.VerificationRecordValue!.Length);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync($"/api/tenancy/tls-authorization?domain={domain.Hostname}")).StatusCode);
        using var visitor = app.Server.CreateClient();
        SetHost(visitor, domain.Hostname);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/marketplace/context?host={domain.Hostname}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"{path}/{domain.Id}", new UpdateTenantDomainRequest(true, true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{path}/{domain.Id}/verify", new { })).StatusCode);

        app.Server.Services.GetRequiredService<TestDomainOwnership>().ExpectedToken = domain.VerificationRecordValue[7..];
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"{path}/{domain.Id}/verify", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync($"/api/tenancy/tls-authorization?domain={domain.Hostname}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync($"/api/marketplace/context?host={domain.Hostname}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"{path}/{domain.Id}", new UpdateTenantDomainRequest(true, true))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"{path}/{domain.Id}", new UpdateTenantDomainRequest(false, false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync($"/api/tenancy/tls-authorization?domain={domain.Hostname}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/marketplace/context?host={domain.Hostname}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{path}/{domain.Id}")).StatusCode);
    }

    [Fact]
    public async Task Managed_domains_are_unique_reserved_hosts_rejected_and_tenants_cannot_manage_platform_domains()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var first = await app.AddTenantAsync();
        var second = await app.AddTenantAsync();
        using var admin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin)));
        var path = $"/api/platform/tenants/{first}/domains";
        foreach (var host in new[] { "faunex.co.za", "www.faunex.co.za", "api.faunex.co.za", "127.0.0.1" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path, new CreateTenantDomainRequest(host))).StatusCode);
        var created = await admin.PostAsJsonAsync(path, new CreateTenantDomainRequest("stormbird.faunex.co.za", true));
        var domain = (await created.Content.ReadFromJsonAsync<TenantDomainDto>())!;
        Assert.NotNull(domain.VerifiedAt);
        Assert.True(domain.IsPrimary);
        Assert.Null(domain.VerificationRecordValue);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/platform/tenants/{second}/domains", new CreateTenantDomainRequest(domain.Hostname))).StatusCode);
        using var tenantAdmin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(first, FaunexRoles.TenantAdmin)));
        Assert.Equal(HttpStatusCode.Forbidden, (await tenantAdmin.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/platform/tenants/{second}/domains/{domain.Id}")).StatusCode);
    }

    [Fact]
    public async Task Expired_or_rotated_DNS_challenge_cannot_verify_and_inactive_tenant_cannot_receive_TLS()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        using var admin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin)));
        var path = $"/api/platform/tenants/{tenant}/domains";
        var domain = (await (await admin.PostAsJsonAsync(path, new CreateTenantDomainRequest("auction.example.co.za"))).Content.ReadFromJsonAsync<TenantDomainDto>())!;
        app.Server.Services.GetRequiredService<TestDomainOwnership>().ExpectedToken = domain.VerificationRecordValue![7..];
        using (var scope = app.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.TenantDomains.SingleAsync(x => x.Id == domain.Id)).VerificationRequestedAt = DateTimeOffset.UtcNow.AddDays(-8);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{path}/{domain.Id}/verify", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"{path}/{domain.Id}/challenge", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{path}/{domain.Id}/verify", new { })).StatusCode);
        using (var scope = app.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Tenants.SingleAsync(x => x.Id == tenant)).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync($"/api/tenancy/tls-authorization?domain={AccountApiFixture.TenantHost(tenant)}")).StatusCode);
    }

    [Fact]
    public async Task Accounts_and_public_host_queries_cannot_cross_marketplaces()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var first = await app.AddTenantAsync();
        var second = await app.AddTenantAsync();
        var buyer = await app.AddUserAsync(first, FaunexRoles.Buyer);
        using var client = app.AuthenticatedClient(await app.TokenAsync(buyer));
        SetHost(client, AccountApiFixture.TenantHost(second));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/auth/login", new { buyer.Email, AccountApiFixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.GetAsync($"/api/marketplace/context?host={AccountApiFixture.TenantHost(first)}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/auth/register", new { Email = "wrong@example.test", AccountApiFixture.Password, RegistrationHost = AccountApiFixture.TenantHost(first) })).StatusCode);
        var register = await app.Client.PostAsJsonAsync("/api/auth/register", new { Email = "buyer@example.test", AccountApiFixture.Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.Equal(second, (await register.Content.ReadFromJsonAsync<AuthResponse>())!.TenantId);
        SetHost(app.Client, MarketplaceHosts.Root);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/auth/register", new { Email = "platform@example.test", AccountApiFixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/auth/login", new { buyer.Email, AccountApiFixture.Password })).StatusCode);
        var tenantAdmin = await app.AddUserAsync(first, FaunexRoles.TenantAdmin);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.PostAsJsonAsync("/api/auth/login", new { tenantAdmin.Email, AccountApiFixture.Password })).StatusCode);
        SetHost(app.Client, AccountApiFixture.TenantHost(second));
        var platform = await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/auth/login", new { platform.Email, AccountApiFixture.Password })).StatusCode);
    }

    [Fact]
    public async Task Reset_links_stay_on_verified_tenant_and_wrong_tenant_cannot_send_or_consume_reset()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var first = await app.AddTenantAsync();
        var second = await app.AddTenantAsync();
        var buyer = await app.AddUserAsync(first, FaunexRoles.Buyer);
        var emails = app.Server.Services.GetRequiredService<TestEmailSender>();
        Assert.Equal(HttpStatusCode.OK, (await app.Client.PostAsJsonAsync("/api/auth/forgot-password", new { buyer.Email })).StatusCode);
        Assert.Empty(emails.ResetLinks);
        SetHost(app.Client, AccountApiFixture.TenantHost(first));
        var response = await app.Client.PostAsJsonAsync("/api/auth/forgot-password", new { buyer.Email });
        var reset = (await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>())!;
        Assert.StartsWith($"https://{AccountApiFixture.TenantHost(first)}/reset-password?", Assert.Single(emails.ResetLinks));
        SetHost(app.Client, AccountApiFixture.TenantHost(second));
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/auth/reset-password", new { buyer.Email, reset.ResetToken, NewPassword = "Changed!Password123" })).StatusCode);
        SetHost(app.Client, AccountApiFixture.TenantHost(first));
        Assert.Equal(HttpStatusCode.NoContent, (await app.Client.PostAsJsonAsync("/api/auth/reset-password", new { buyer.Email, reset.ResetToken, NewPassword = "Changed!Password123" })).StatusCode);
    }

    [Fact]
    public async Task Rotating_challenge_while_DNS_is_in_flight_cannot_verify_stale_proof()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        using var admin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin)));
        var path = $"/api/platform/tenants/{tenant}/domains";
        var domain = (await (await admin.PostAsJsonAsync(path, new CreateTenantDomainRequest("auction.example.co.za"))).Content.ReadFromJsonAsync<TenantDomainDto>())!;
        app.Server.Services.GetRequiredService<TestDomainOwnership>().Handler = async () =>
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"{path}/{domain.Id}/challenge", new { })).StatusCode);
            return true;
        };
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{path}/{domain.Id}/verify", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync($"/api/tenancy/tls-authorization?domain={domain.Hostname}")).StatusCode);
    }

    [Theory]
    [InlineData("unknown.example.co.za")]
    [InlineData("https://faunex.co.za/path")]
    [InlineData("faunex.co.za,other.example.co.za")]
    public async Task Unknown_or_malformed_origin_is_closed(string host)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        SetHost(app.Client, host);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/api/marketplace/context?host=faunex.co.za")).StatusCode);
    }

    private static void SetHost(HttpClient client, string host)
    {
        client.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader);
        client.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, host);
    }
}
