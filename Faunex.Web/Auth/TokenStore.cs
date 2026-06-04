using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace Faunex.Web.Auth;

public sealed class TokenStore(ProtectedLocalStorage storage)
{
    private const string Key = "faunex.jwt";
    private string? _cachedToken;

    public async Task<string?> GetTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedToken))
        {
            return _cachedToken;
        }

        try
        {
            var result = await storage.GetAsync<string>(Key);
            _cachedToken = result.Success ? result.Value : null;
            return _cachedToken;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        _cachedToken = token;
        await storage.SetAsync(Key, token);
    }

    public async Task ClearAsync()
    {
        _cachedToken = null;
        await storage.DeleteAsync(Key);
    }
}
