using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Faunex.Web.Auth;

public static class JwtPrincipalReader
{
    // UI state only; the API independently verifies the signature and current account/session.
    public static ClaimsPrincipal Read(string? token, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                if (jwt.ValidTo > now.UtcDateTime && jwt.ValidFrom <= now.UtcDateTime)
                {
                    var claims = jwt.Claims.ToList();
                    claims.AddRange(jwt.Claims.Where(x => x.Type == "role").Select(x => new Claim(ClaimTypes.Role, x.Value)));
                    var email = jwt.Claims.FirstOrDefault(x => x.Type == "email")?.Value;
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        claims.Add(new Claim(ClaimTypes.Email, email));
                    }

                    return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", ClaimTypes.Email, ClaimTypes.Role));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or SecurityTokenException) { }
        }

        return new ClaimsPrincipal(new ClaimsIdentity());
    }
}
