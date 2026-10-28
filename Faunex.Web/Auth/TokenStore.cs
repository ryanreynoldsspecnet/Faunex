using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace Faunex.Web.Auth;

public sealed class TokenStore(ProtectedLocalStorage storage)
{
    private const string Key = "faunex.jwt";
    private string? _cachedToken;
    private bool _cleared;
    public event Action? Cleared;

    public async Task<string?> GetTokenAsync()
    {
        if (_cleared)
        {
            return null;
        }
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
        await storage.SetAsync(Key, token);
        _cachedToken = token;
        _cleared = false;
    }

    public async Task ClearAsync()
    {
        _cachedToken = null;
        _cleared = true;
        try
        {
            await storage.DeleteAsync(Key);
        }
        catch (InvalidOperationException) { }
        catch (JSException) { }
        Cleared?.Invoke();
    }
}
