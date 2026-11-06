using System.Net;
using System.Net.Http.Json;
using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Faunex.Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

namespace Faunex.Tests;

public sealed class AdminMfaTests
{
    private const string Login = "/api/auth/login";
    private const string Security = "/api/auth/mfa";

    [Theory]
    [InlineData(FaunexRoles.PlatformSuperAdmin)]
    [InlineData(FaunexRoles.TenantAdmin)]
    public async Task Optional_enrollment_revokes_old_session_and_then_requires_a_nonreplayed_code(string role)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = role == FaunexRoles.TenantAdmin ? await app.AddTenantAsync() : (Guid?)null;
        var user = await app.AddUserAsync(tenant, role);
        using var anonymous = Anonymous(app, tenant);
        var first = await Post<AuthResponse>(anonymous, Login, new LoginRequest(user.Email!, AccountApiFixture.Password));
        using var admin = app.AuthenticatedClient(first.AccessToken);
        Assert.False((await admin.GetFromJsonAsync<AdminMfaStatus>(Security))!.Enabled);
        var setup = await Begin(admin);
        Assert.StartsWith("iVBOR", setup.QrPngBase64);
        var clock = Clock(app);
        var codes = await Confirm(admin, setup, clock);
        Assert.Equal(8, codes.Codes.Count);
        Assert.Equal(8, codes.Codes.Distinct().Count());
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/auth/me")).StatusCode);
        using (var scope = app.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();
            var stored = await db.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id);
            Assert.NotNull(stored.MfaProtectedKey);
            Assert.DoesNotContain(setup.ManualKey, stored.MfaProtectedKey);
            Assert.DoesNotContain(codes.Codes[0].Replace("-", ""), stored.MfaRecoveryHashes!);
        }

        var challenge = await Challenge(anonymous, user);
        using var replayEnrollment = await anonymous.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(challenge, Code(setup, clock)));
        Assert.Equal(HttpStatusCode.BadRequest, replayEnrollment.StatusCode);
        Advance(clock, 30);
        var loggedIn = await Complete(anonymous, challenge, Code(setup, clock));
        using var verified = app.AuthenticatedClient(loggedIn.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await verified.GetAsync("/api/auth/me")).StatusCode);
        Assert.True((await verified.GetFromJsonAsync<AdminMfaStatus>(Security))!.Enabled);
        var freshChallenge = await Challenge(anonymous, user);
        using var sameCode = await anonymous.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(freshChallenge, Code(setup, clock)));
        Assert.Equal(HttpStatusCode.BadRequest, sameCode.StatusCode);
        Advance(clock, 30);
        using var spentChallenge = await anonymous.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(challenge, Code(setup, clock)));
        Assert.Equal(HttpStatusCode.BadRequest, spentChallenge.StatusCode);
    }

    [Fact]
    public async Task Lost_device_recovery_and_replacement_cannot_be_bypassed_by_email_password_reset()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        var user = await app.AddUserAsync(tenant, FaunexRoles.TenantAdmin);
        using var anonymous = Anonymous(app, tenant);
        using var admin = app.AuthenticatedClient(await app.TokenAsync(user));
        var clock = Clock(app);
        var original = await Begin(admin);
        var codes = await Confirm(admin, original, clock);
        var challengeBeforeReset = await Challenge(anonymous, user);
        var reset = await Post<ForgotPasswordResponse>(anonymous, "/api/auth/forgot-password", new ForgotPasswordRequest(user.Email!));
        const string newPassword = "New!TestPassword1234";
        (await anonymous.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(user.Email!, reset.ResetToken!, newPassword))).EnsureSuccessStatusCode();
        using var invalidated = await anonymous.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(challengeBeforeReset, codes.Codes[0], true));
        Assert.Equal(HttpStatusCode.BadRequest, invalidated.StatusCode);
        var challenge = await Challenge(anonymous, user, newPassword);
        var recovered = await Complete(anonymous, challenge, codes.Codes[0], recovery: true);
        using var recoverySession = app.AuthenticatedClient(recovered.AccessToken);
        Assert.Equal(7, (await recoverySession.GetFromJsonAsync<AdminMfaStatus>(Security))!.RecoveryCodesRemaining);
        var unusedChallenge = await Challenge(anonymous, user, newPassword);
        using var spent = await anonymous.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(unusedChallenge, codes.Codes[0], true));
        Assert.Equal(HttpStatusCode.BadRequest, spent.StatusCode);
        var replacement = await Begin(recoverySession, newPassword);
        Assert.NotEqual(original.ManualKey, replacement.ManualKey);
        var newCodes = await Confirm(recoverySession, replacement, clock);
        Assert.DoesNotContain(codes.Codes[1], newCodes.Codes);
        Assert.Equal(HttpStatusCode.Unauthorized, (await recoverySession.GetAsync("/api/auth/me")).StatusCode);
        var next = await Challenge(anonymous, user, newPassword);
        using var previousRecovery = await anonymous.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(next, codes.Codes[1], true));
        Assert.Equal(HttpStatusCode.BadRequest, previousRecovery.StatusCode);
        Advance(clock, 30);
        var login = await Complete(anonymous, next, Code(replacement, clock));
        using var verified = app.AuthenticatedClient(login.AccessToken);
        Assert.Equal(8, (await verified.GetFromJsonAsync<AdminMfaStatus>(Security))!.RecoveryCodesRemaining);
        Advance(clock, 601);
        Assert.Equal(HttpStatusCode.BadRequest, (await verified.PostAsJsonAsync(Security + "/setup", new AdminMfaStart(newPassword))).StatusCode);
    }

    [Fact]
    public async Task Second_factor_attempt_budget_survives_new_challenges_and_is_bound_to_marketplace()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        var foreign = await app.AddTenantAsync();
        var user = await app.AddUserAsync(tenant, FaunexRoles.TenantAdmin);
        using var anonymous = Anonymous(app, tenant);
        using var wrongHost = Anonymous(app, foreign);
        using var admin = app.AuthenticatedClient(await app.TokenAsync(user));
        var buyer = await app.AddUserAsync(tenant, FaunexRoles.Buyer);
        using var buyerSession = app.AuthenticatedClient(await app.TokenAsync(buyer));
        Assert.Equal(HttpStatusCode.Forbidden, (await buyerSession.GetAsync(Security)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Security + "/setup", new AdminMfaStart("wrong"))).StatusCode);
        var clock = Clock(app);
        var setup = await Begin(admin);
        var codes = await Confirm(admin, setup, clock);
        var challenge = await Challenge(anonymous, user);
        using var mismatched = await wrongHost.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(challenge, codes.Codes[0], true));
        Assert.Equal(HttpStatusCode.BadRequest, mismatched.StatusCode);
        for (var i = 0; i < 5; i++)
        {
            var fresh = await Challenge(anonymous, user);
            using var attempt = await anonymous.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(fresh, "not a recovery code", true));
            Assert.Equal(HttpStatusCode.BadRequest, attempt.StatusCode);
        }
        var stillLocked = await Challenge(anonymous, user);
        using var locked = await anonymous.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(stillLocked, codes.Codes[0], true));
        Assert.Equal(HttpStatusCode.BadRequest, locked.StatusCode);
        Advance(clock, 301);
        using var expired = await anonymous.PostAsJsonAsync(Login + "/mfa", new AdminMfaLogin(stillLocked, codes.Codes[0], true));
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        await Complete(anonymous, await Challenge(anonymous, user), codes.Codes[0], recovery: true);
    }

    private static HttpClient Anonymous(AccountApiFixture app, Guid? tenant)
    {
        var client = app.Server.CreateClient();
        client.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, tenant is { } id ? AccountApiFixture.TenantHost(id) : MarketplaceHosts.Root);
        return client;
    }

    [PostgresFact]
    public async Task Concurrent_redemption_of_a_recovery_code_issues_only_one_session()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin);
        using var first = Anonymous(app, null);
        using var second = Anonymous(app, null);
        using var admin = app.AuthenticatedClient(await app.TokenAsync(user));
        var codes = await Confirm(admin, await Begin(admin), Clock(app));
        var request = new AdminMfaLogin(await Challenge(first, user), codes.Codes[0], true);
        var outcomes = await Task.WhenAll(first.PostAsJsonAsync(Login + "/mfa", request), second.PostAsJsonAsync(Login + "/mfa", request));
        Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.BadRequest);
    }
    private static TestAuctionClock Clock(AccountApiFixture app) => app.Server.Services.GetRequiredService<TestAuctionClock>();
    private static void Advance(TestAuctionClock clock, int seconds) => clock.Set(clock.GetUtcNow().AddSeconds(seconds));
    private static string Code(AdminMfaSetup setup, TestAuctionClock clock) =>
        new Totp(Base32Encoding.ToBytes(setup.ManualKey)).ComputeTotp(clock.GetUtcNow().UtcDateTime);
    private static Task<AdminMfaSetup> Begin(HttpClient client, string password = AccountApiFixture.Password) =>
        Post<AdminMfaSetup>(client, Security + "/setup", new AdminMfaStart(password));
    private static Task<AdminMfaRecovery> Confirm(HttpClient client, AdminMfaSetup setup, TestAuctionClock clock) =>
        Post<AdminMfaRecovery>(client, Security + "/confirm", new AdminMfaConfirm(setup.Ticket, Code(setup, clock)));
    private static Task<AuthResponse> Complete(HttpClient client, string challenge, string code, bool recovery = false) =>
        Post<AuthResponse>(client, Login + "/mfa", new AdminMfaLogin(challenge, code, recovery));
    private static async Task<string> Challenge(HttpClient client, ApplicationUser user, string password = AccountApiFixture.Password)
    {
        using var response = await client.PostAsJsonAsync(Login, new LoginRequest(user.Email!, password));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        return (await response.Content.ReadFromJsonAsync<AdminMfaChallenge>())!.MfaChallenge;
    }
    private static async Task<T> Post<T>(HttpClient client, string path, object request)
    {
        using var response = await client.PostAsJsonAsync(path, request);
        Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
