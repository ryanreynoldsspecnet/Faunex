using System.Net;
using System.Net.Http.Json;
using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Faunex.Tests;

public sealed class AccountAccessIntegrationTests
{
    [Theory]
    [InlineData("legacy-no-stamp")]
    [InlineData("wrong-tenant")]
    [InlineData("wrong-platform-flag")]
    [InlineData("wrong-role")]
    [InlineData("expired")]
    public async Task Signed_but_obsolete_or_inconsistent_token_is_rejected(string change)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(await app.AddTenantAsync(), FaunexRoles.Buyer);
        var handler = new JwtSecurityTokenHandler();
        var claims = handler.ReadJwtToken(await app.TokenAsync(user)).Claims
            .Where(x => x.Type is not ("iss" or "aud" or "nbf" or "exp" or "iat")).ToList();
        if (change == "legacy-no-stamp") claims.RemoveAll(x => x.Type == FaunexClaimTypes.SecurityStamp);
        if (change == "wrong-tenant")
        {
            claims.RemoveAll(x => x.Type == FaunexClaimTypes.TenantId);
            claims.Add(new Claim(FaunexClaimTypes.TenantId, Guid.NewGuid().ToString()));
        }
        if (change == "wrong-platform-flag")
        {
            claims.RemoveAll(x => x.Type == FaunexClaimTypes.IsPlatformAdmin);
            claims.Add(new Claim(FaunexClaimTypes.IsPlatformAdmin, "true"));
        }
        if (change == "wrong-role")
        {
            claims.RemoveAll(x => x.Type == ClaimTypes.Role);
            claims.Add(new Claim(ClaimTypes.Role, FaunexRoles.TenantAdmin));
        }
        var jwt = new JwtSecurityToken(AccountApiFixture.Issuer, AccountApiFixture.Issuer, claims,
            notBefore: DateTime.UtcNow.AddHours(-1), expires: DateTime.UtcNow.AddMinutes(change == "expired" ? -10 : 10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AccountApiFixture.SigningKey)), SecurityAlgorithms.HmacSha256));
        using var client = app.AuthenticatedClient(handler.WriteToken(jwt));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Active_account_can_login_and_call_authenticated_api()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(await app.AddTenantAsync(), FaunexRoles.Buyer);
        var response = await app.Client.PostAsJsonAsync("/api/auth/login", new { user.Email, AccountApiFixture.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("deleted")]
    [InlineData("password-reset")]
    [InlineData("roles")]
    [InlineData("reassigned")]
    [InlineData("tenant-inactive")]
    [InlineData("tenant-deleted")]
    [InlineData("stamp")]
    [InlineData("email")]
    public async Task Existing_token_is_rejected_after_security_change(string change)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenantId = await app.AddTenantAsync();
        var user = await app.AddUserAsync(tenantId, FaunexRoles.Buyer);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));

        if (change.StartsWith("tenant-"))
        {
            using var scope = app.Server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
            if (change == "tenant-deleted") db.Tenants.Remove(tenant);
            else tenant.IsActive = false;
            await db.SaveChangesAsync();
        }
        else
        {
            var otherTenant = change == "reassigned" ? await app.AddTenantAsync() : tenantId;
            await app.ChangeUserAsync(user.Id, async (users, current) =>
            {
                switch (change)
                {
                    case "disabled":
                        current.LockoutEnd = DateTimeOffset.MaxValue;
                        Assert.True((await users.UpdateAsync(current)).Succeeded);
                        break;
                    case "deleted": Assert.True((await users.DeleteAsync(current)).Succeeded); break;
                    case "password-reset":
                        var resetToken = await users.GeneratePasswordResetTokenAsync(current);
                        var response = await app.Client.PostAsJsonAsync("/api/auth/reset-password",
                            new { current.Email, ResetToken = resetToken, NewPassword = "Replacement!123" });
                        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                        break;
                    case "roles": Assert.True((await users.AddToRoleAsync(current, FaunexRoles.Seller)).Succeeded); break;
                    case "reassigned":
                        current.TenantId = otherTenant;
                        Assert.True((await users.UpdateAsync(current)).Succeeded);
                        break;
                    case "stamp": Assert.True((await users.UpdateSecurityStampAsync(current)).Succeeded); break;
                    case "email": Assert.True((await users.SetEmailAsync(current, "changed@example.test")).Succeeded); break;
                }
            });
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Disabled_account_or_inactive_tenant_cannot_login(bool disableAccount)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(await app.AddTenantAsync(active: disableAccount), FaunexRoles.Buyer);
        if (disableAccount)
        {
            await app.ChangeUserAsync(user.Id, async (users, current) =>
            {
                current.LockoutEnd = DateTimeOffset.MaxValue;
                // Manual disabling must work even on older records without Identity lockout enabled.
                current.LockoutEnabled = false;
                Assert.True((await users.UpdateAsync(current)).Succeeded);
            });
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/auth/login",
            new { user.Email, AccountApiFixture.Password })).StatusCode);
    }

    [Fact]
    public async Task Five_failed_passwords_temporarily_lock_account_and_success_resets_counter()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(null, FaunexRoles.PlatformAdmin);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/auth/login",
                new { user.Email, Password = "wrong" })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await app.Client.PostAsJsonAsync("/api/auth/login",
            new { user.Email, AccountApiFixture.Password })).StatusCode);
        for (var i = 0; i < 5; i++)
        {
            await app.Client.PostAsJsonAsync("/api/auth/login", new { user.Email, Password = "wrong" });
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsJsonAsync("/api/auth/login",
            new { user.Email, AccountApiFixture.Password })).StatusCode);
    }
}
