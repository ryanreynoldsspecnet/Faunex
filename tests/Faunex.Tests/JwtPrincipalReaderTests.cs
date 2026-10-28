using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Faunex.Web.Auth;

namespace Faunex.Tests;

public sealed class JwtPrincipalReaderTests
{
    [Theory]
    [InlineData(-1, 1, true)]
    [InlineData(-2, -1, false)]
    [InlineData(1, 2, false)]
    public void Ui_identity_respects_token_lifetime(int starts, int ends, bool authenticated)
    {
        var now = DateTimeOffset.UtcNow;
        var token = new JwtSecurityToken(claims: [new Claim("email", "reader@example.test"), new Claim("role", "Buyer")],
            notBefore: now.AddHours(starts).UtcDateTime, expires: now.AddHours(ends).UtcDateTime);
        var principal = JwtPrincipalReader.Read(new JwtSecurityTokenHandler().WriteToken(token), now);
        Assert.Equal(authenticated, principal.Identity!.IsAuthenticated);
        if (authenticated)
        {
            Assert.Equal("reader@example.test", principal.Identity.Name);
            Assert.True(principal.IsInRole("Buyer"));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-token")]
    public void Missing_or_malformed_token_is_anonymous(string? token) =>
        Assert.False(JwtPrincipalReader.Read(token, DateTimeOffset.UtcNow).Identity!.IsAuthenticated);
}
