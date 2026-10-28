using System.Net;
using System.Net.Http.Json;
using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class TenantOnboardingIntegrationTests
{
    [Theory]
    [InlineData("success", HttpStatusCode.Created)]
    [InlineData("password", HttpStatusCode.BadRequest)]
    [InlineData("role-result", HttpStatusCode.BadRequest)]
    [InlineData("exception-after-role-write", HttpStatusCode.InternalServerError)]
    public async Task Tenant_and_first_admin_commit_or_rollback_together(string failure, HttpStatusCode expected)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var admin = await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin);
        using var client = app.AuthenticatedClient(await app.TokenAsync(admin));
        var injected = app.Server.Services.GetRequiredService<OnboardingFailure>();
        injected.ReturnRoleFailure = failure == "role-result";
        injected.ThrowAfterRoleAssignment = failure == "exception-after-role-write";
        const string email = "first-admin@example.test";
        var response = await client.PostAsJsonAsync("/api/platform/tenants", new
        {
            Name = "Onboarding test",
            FirstAdminEmail = email,
            FirstAdminPassword = failure == "password" ? "x" : AccountApiFixture.Password
        });
        Assert.Equal(expected, response.StatusCode);

        using var scope = app.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var identity = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Name == "Onboarding test");
        var user = await identity.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (failure == "success")
        {
            Assert.NotNull(tenant);
            Assert.NotNull(user);
            Assert.Equal(tenant.Id, user.TenantId);
            Assert.Single(await identity.UserRoles.Where(x => x.UserId == user.Id).ToListAsync());
        }
        else
        {
            Assert.Null(tenant);
            Assert.Null(user);
            Assert.Single(await identity.UserRoles.ToListAsync()); // Only the pre-existing platform admin role.
        }
    }
}
