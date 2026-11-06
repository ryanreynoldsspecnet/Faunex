using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Faunex.Api.Tenancy;
using Faunex.Application.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using OtpNet;
using QRCoder;

namespace Faunex.Api.Auth;

public sealed class AdminMfaService(UserManager<ApplicationUser> users, AccountAccessService accounts,
    TenantRequestContext origin, IDataProtectionProvider protection, TimeProvider clock, ILogger<AdminMfaService> logger)
{
    private const int MaximumFailures = 5;
    private readonly IDataProtector _tickets = protection.CreateProtector("Faunex.AdminMfa.Ticket.v1");
    private readonly IDataProtector _keys = protection.CreateProtector("Faunex.AdminMfa.Key.v1");

    public AdminMfaStatus Status(ApplicationUser user) =>
        new(user.MfaProtectedKey is not null, ReadHashes(user).Count);

    public string Challenge(ApplicationUser user) => WriteTicket(user, "login", null, null, TimeSpan.FromMinutes(5));

    public async Task<ApplicationUser?> CompleteLoginAsync(AdminMfaLogin request, CancellationToken ct)
    {
        var ticket = ReadTicket(request.Challenge, "login");
        if (ticket is null) return null;
        var user = await users.FindByIdAsync(ticket.Actor.ToString());
        if (user is null || user.MfaProtectedKey is null || !Current(ticket, user)
            || !await accounts.CanSignInAsync(user, ct)
            || !await origin.AllowsAccountAsync(user.TenantId, user.IsPlatformAdmin,
                await users.IsInRoleAsync(user, FaunexRoles.TenantAdmin), ct)
            || !await ChargeAttemptAsync(user)) return null;

        if (request.RecoveryCode)
        {
            var hashes = ReadHashes(user);
            var normalized = request.Code?.Trim().Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
            var index = normalized is { Length: 32 } && normalized.All(Uri.IsHexDigit)
                ? hashes.FindIndex(x => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(x), HashCode(normalized)))
                : -1;
            if (index < 0) return null;
            hashes.RemoveAt(index);
            user.MfaRecoveryHashes = JsonSerializer.Serialize(hashes);
        }
        else
        {
            string key;
            try { key = _keys.CreateProtector(user.Id.ToString("N")).Unprotect(user.MfaProtectedKey); }
            catch (CryptographicException)
            {
                logger.LogError("Admin MFA secret could not be decrypted for account {ActorId}", user.Id);
                return null;
            }
            if (!Verify(key, request.Code, out var step) || step <= user.MfaLastStep) return null;
            user.MfaLastStep = step;
        }

        // One successful completion invalidates all outstanding password challenges for this account.
        user.MfaLoginNonce = Guid.NewGuid();
        ClearFailures(user);
        var saved = request.RecoveryCode ? await users.UpdateSecurityStampAsync(user) : await users.UpdateAsync(user);
        if (!saved.Succeeded) return null;
        if (request.RecoveryCode) logger.LogInformation("Admin MFA recovery code redeemed for account {ActorId}", user.Id);
        return user;
    }

    public async Task<AdminMfaSetup?> BeginAsync(ApplicationUser user, ClaimsPrincipal session, string password, CancellationToken ct)
    {
        var sessionId = session.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (string.IsNullOrEmpty(sessionId) || !CanManage(user, session) || IsThrottled(user)
            || string.IsNullOrEmpty(password) || !await accounts.CheckPasswordAsync(user, password, ct)) return null;
        var key = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
        var uri = new OtpUri(OtpType.Totp, key, user.Email ?? user.Id.ToString(), "Faunex").ToString();
        using var data = QRCodeGenerator.GenerateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return new(WriteTicket(user, "setup", key, sessionId, TimeSpan.FromMinutes(10)), key, Convert.ToBase64String(png.GetGraphic(5)));
    }

    public async Task<AdminMfaRecovery?> ConfirmAsync(ApplicationUser user, ClaimsPrincipal session, AdminMfaConfirm request)
    {
        var ticket = ReadTicket(request.Ticket, "setup");
        if (ticket is null || ticket.Key is null || !Current(ticket, user)
            || ticket.Session != session.FindFirstValue(JwtRegisteredClaimNames.Jti)
            || !CanManage(user, session) || !await ChargeAttemptAsync(user)) return null;
        if (!Verify(ticket.Key, request.Code, out var step)) return null;

        var codes = Enumerable.Range(0, 8).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(16))).ToArray();
        user.MfaRecoveryHashes = JsonSerializer.Serialize(codes.Select(x => Convert.ToHexString(HashCode(x))));
        user.MfaProtectedKey = _keys.CreateProtector(user.Id.ToString("N")).Protect(ticket.Key);
        user.MfaLastStep = step;
        user.MfaLoginNonce = Guid.NewGuid();
        ClearFailures(user);
        // Identity's concurrency stamp makes rotation atomic with the key and recovery codes.
        if (!(await users.UpdateSecurityStampAsync(user)).Succeeded) return null;
        logger.LogInformation("Admin MFA enrolled or replaced for account {ActorId}; previous sessions revoked", user.Id);
        return new(codes.Select(x => $"{x[..8]}-{x[8..16]}-{x[16..24]}-{x[24..]}").ToArray());
    }

    private bool CanManage(ApplicationUser user, ClaimsPrincipal session)
    {
        if (user.MfaProtectedKey is null) return true;
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        return long.TryParse(session.FindFirstValue(FaunexClaimTypes.MfaVerifiedAt), NumberStyles.None, CultureInfo.InvariantCulture, out var verified)
            && verified <= now && verified >= now - 600;
    }

    private bool Verify(string key, string? code, out long step)
    {
        step = -1;
        var trimmed = code?.Trim();
        return trimmed is { Length: 6 } && trimmed.All(char.IsAsciiDigit)
            && new Totp(Base32Encoding.ToBytes(key)).VerifyTotp(clock.GetUtcNow().UtcDateTime, trimmed, out step, new VerificationWindow(previous: 1));
    }

    private bool IsThrottled(ApplicationUser user) => user.MfaFailures >= MaximumFailures && user.MfaFailureWindowEnd > clock.GetUtcNow();
    private async Task<bool> ChargeAttemptAsync(ApplicationUser user)
    {
        if (IsThrottled(user)) return false;
        var now = clock.GetUtcNow();
        if (user.MfaFailureWindowEnd is null || user.MfaFailureWindowEnd <= now)
        {
            user.MfaFailureWindowEnd = now.AddMinutes(5);
            user.MfaFailures = 0;
        }
        user.MfaFailures++;
        // Count before verifying, so concurrent guesses cannot bypass the durable attempt budget.
        return (await users.UpdateAsync(user)).Succeeded;
    }
    private static void ClearFailures(ApplicationUser user) { user.MfaFailures = 0; user.MfaFailureWindowEnd = null; }
    private static byte[] HashCode(string code) => SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(code));
    private static List<string> ReadHashes(ApplicationUser user) => user.MfaRecoveryHashes is { } json
        ? JsonSerializer.Deserialize<List<string>>(json) ?? [] : [];

    private string WriteTicket(ApplicationUser user, string kind, string? key, string? session, TimeSpan duration) =>
        _tickets.Protect(JsonSerializer.Serialize(new Ticket(user.Id, user.SecurityStamp!, user.MfaLoginNonce,
            origin.Host!, kind, clock.GetUtcNow().Add(duration), key, session)));

    private Ticket? ReadTicket(string? value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096) return null;
        try
        {
            var ticket = JsonSerializer.Deserialize<Ticket>(_tickets.Unprotect(value));
            return ticket is not null && ticket.Kind == kind && ticket.Host == origin.Host && ticket.Expires > clock.GetUtcNow()
                ? ticket : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return null; }
    }

    private static bool Current(Ticket ticket, ApplicationUser user) =>
        ticket.Actor == user.Id && ticket.Stamp == user.SecurityStamp && ticket.Nonce == user.MfaLoginNonce;
    private sealed record Ticket(Guid Actor, string Stamp, Guid? Nonce, string Host, string Kind, DateTimeOffset Expires, string? Key, string? Session);
}
