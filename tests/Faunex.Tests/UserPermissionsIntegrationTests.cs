using System.Net;
using System.Net.Http.Json;
using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class UserPermissionsIntegrationTests
{
    [Fact]
    public async Task Seller_cannot_spoof_another_seller_identity()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(await app.AddTenantAsync(), FaunexRoles.Seller);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/seller/listings?sellerId={Guid.NewGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData(FaunexRoles.TenantAdmin, HttpStatusCode.Created)]
    [InlineData(FaunexRoles.Seller, HttpStatusCode.Created)]
    [InlineData(FaunexRoles.Buyer, HttpStatusCode.Forbidden)]
    [InlineData(FaunexRoles.PlatformSuperAdmin, HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task Listing_creation_uses_the_intended_role_and_real_trading_profile(string? role, HttpStatusCode expected)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var actorId = Guid.NewGuid();
        var token = string.Empty;
        if (role is not null)
        {
            var tenantId = role == FaunexRoles.PlatformSuperAdmin ? (Guid?)null : await app.AddTenantAsync();
            var user = await app.AddUserAsync(tenantId, role);
            actorId = user.Id;
            token = await app.TokenAsync(user);
            // Repeated token issuance must not duplicate the trading profile.
            await app.TokenAsync(user);
        }
        using var client = app.AuthenticatedClient(token);
        var response = await client.PostAsJsonAsync("/api/seller/listings/bird", new
        {
            SpeciesId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Title = "Authorized draft", Price = 100m, IsAuction = true, SellerId = actorId
        });
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(FaunexRoles.TenantAdmin, HttpStatusCode.BadRequest)]
    [InlineData(FaunexRoles.Seller, HttpStatusCode.Forbidden)]
    [InlineData(FaunexRoles.Buyer, HttpStatusCode.Forbidden)]
    public async Task Only_tenant_admin_can_open_and_close_auctions(string role, HttpStatusCode expected)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(await app.AddTenantAsync(), role);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));
        // Missing auction produces 400 only after the correct role passes authorization.
        Assert.Equal(expected, (await client.PostAsJsonAsync($"/api/auctions/{Guid.NewGuid()}/open", new { })).StatusCode);
        Assert.Equal(expected, (await client.PostAsJsonAsync($"/api/auctions/{Guid.NewGuid()}/close", new { })).StatusCode);
    }

    [Theory]
    [InlineData("PlatformSuperAdmin")]
    [InlineData("platformsuperadmin")]
    public async Task Platform_admin_cannot_promote_self_or_create_super_admin(string role)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(null, FaunexRoles.PlatformAdmin);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/platform/users/{user.Id}/roles", new { Roles = new[] { role } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/platform/users",
            new { Email = "promoted@example.test", AccountApiFixture.Password, Roles = new[] { role } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Last_super_admin_cannot_be_demoted_and_cannot_disable_self()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/platform/users/{user.Id}/roles",
            new { Roles = new[] { FaunexRoles.PlatformAdmin } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/platform/users/{user.Id}",
            new { user.Email, Roles = new[] { FaunexRoles.PlatformSuperAdmin }, IsActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Last_tenant_admin_removal_is_guarded_in_parallel_on_Postgres()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var platform = await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin);
        using var client = app.AuthenticatedClient(await app.TokenAsync(platform));
        var tenantId = await app.AddTenantAsync();
        var first = await app.AddUserAsync(tenantId, FaunexRoles.TenantAdmin);
        var second = await app.AddUserAsync(tenantId, FaunexRoles.TenantAdmin);
        HttpResponseMessage[] responses;
        if (app.UsesPostgres)
        {
            responses = await Task.WhenAll(client.DeleteAsync($"/api/platform/users/{first.Id}"), client.DeleteAsync($"/api/platform/users/{second.Id}"));
        }
        else
        {
            // SQLite is the lightweight local fallback; CI exercises PostgreSQL concurrency.
            responses = [await client.DeleteAsync($"/api/platform/users/{first.Id}"), await client.DeleteAsync($"/api/platform/users/{second.Id}")];
        }
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = app.Server.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().GetUsersInRoleAsync(FaunexRoles.TenantAdmin));
    }

    [Fact]
    public async Task Tenant_admin_cannot_access_another_tenants_users()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var admin = await app.AddUserAsync(await app.AddTenantAsync(), FaunexRoles.TenantAdmin);
        var other = await app.AddUserAsync(await app.AddTenantAsync(), FaunexRoles.Buyer);
        using var client = app.AuthenticatedClient(await app.TokenAsync(admin));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tenant/users/{other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/tenant/users/{other.Id}")).StatusCode);
    }

    [Fact]
    public async Task Disabling_then_reenabling_user_does_not_restore_the_old_session()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenantId = await app.AddTenantAsync();
        var admin = await app.AddUserAsync(tenantId, FaunexRoles.TenantAdmin);
        var buyer = await app.AddUserAsync(tenantId, FaunexRoles.Buyer);
        using var client = app.AuthenticatedClient(await app.TokenAsync(admin));
        using var stale = app.AuthenticatedClient(await app.TokenAsync(buyer));
        foreach (var active in new[] { false, true })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/tenant/users/{buyer.Id}",
                new { buyer.Email, Roles = new[] { FaunexRoles.Buyer }, IsActive = active })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await stale.GetAsync("/api/auth/me")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await app.Client.PostAsJsonAsync("/api/auth/login",
            new { buyer.Email, AccountApiFixture.Password })).StatusCode);
    }
}
