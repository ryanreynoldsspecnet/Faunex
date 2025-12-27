using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace Faunex.Web.Auth;

public sealed class TokenStore(ProtectedLocalStorage storage)
{
    private const string Key = "faunex.jwt";

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var result = await storage.GetAsync<string>(Key);
            return result.Success ? result.Value : null;
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
    }

    public async Task ClearAsync()
    {
        await storage.DeleteAsync(Key);
    }
}
