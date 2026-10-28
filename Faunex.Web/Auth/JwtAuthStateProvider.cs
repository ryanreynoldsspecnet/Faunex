using Microsoft.AspNetCore.Components.Authorization;

namespace Faunex.Web.Auth;

public sealed class JwtAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly TokenStore _tokens;

    public JwtAuthStateProvider(TokenStore tokens)
    {
        _tokens = tokens;
        _tokens.Cleared += NotifyUserAuthenticationStateChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetTokenAsync();
        var principal = JwtPrincipalReader.Read(token, DateTimeOffset.UtcNow);
        if (token is not null && principal.Identity?.IsAuthenticated != true)
        {
            await _tokens.ClearAsync();
        }

        return new AuthenticationState(principal);
    }

    public void NotifyUserAuthenticationStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _tokens.Cleared -= NotifyUserAuthenticationStateChanged;
}
