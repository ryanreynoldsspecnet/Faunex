using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Faunex.Web.Auth;

public sealed class TokenStore(ProtectedLocalStorage storage)
{
    private const string Key = "faunex.jwt";

    public async Task<string?> GetTokenAsync()
    {
        var result = await storage.GetAsync<string>(Key);
        return result.Success ? result.Value : null;
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
