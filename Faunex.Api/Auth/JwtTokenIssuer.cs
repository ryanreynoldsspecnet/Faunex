using Faunex.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Faunex.Api.Auth;

public sealed class JwtTokenIssuer(IConfiguration configuration, UserManager<ApplicationUser> users)
{
    public async Task<(string token, DateTimeOffset expiresAt, IReadOnlyList<string> roles)> IssueAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var issuer = GetValueOrDevDefault("FAUNEX_JWT_ISSUER", "faunex-dev");
        var audience = GetValueOrDevDefault("FAUNEX_JWT_AUDIENCE", "faunex-dev");
        var signingKey = GetSigningKey();

        var roles = (await users.GetRolesAsync(user)).ToArray();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(FaunexClaimTypes.IsPlatformAdmin, user.IsPlatformAdmin ? "true" : "false")
        };

        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim(FaunexClaimTypes.TenantId, user.TenantId.Value.ToString()));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);
        // TODO: Reduce expiry for production.

        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return (encoded, expiresAt, roles);
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var raw = configuration["FAUNEX_JWT_SIGNING_KEY"];

        if (string.IsNullOrWhiteSpace(raw))
        {
            // DEV ONLY fallback.
            // IMPORTANT: Set FAUNEX_JWT_SIGNING_KEY in production environments.
            raw = "DEV_ONLY_CHANGE_ME_DEV_ONLY_CHANGE_ME_DEV_ONLY_CHANGE_ME";
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(raw));
    }

    private string GetValueOrDevDefault(string key, string devDefault)
    {
        var value = configuration[key];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // DEV ONLY fallback.
        return devDefault;
    }
}
