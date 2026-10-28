using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Faunex.Web.Api;
using Faunex.Web.Auth;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;

namespace Faunex.Tests;

public sealed class BrowserSessionTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    public async Task Api_revocation_clears_browser_identity_but_permission_denial_does_not(HttpStatusCode status, bool authenticated)
    {
        var storage = new ProtectedLocalStorage(new MemoryJsRuntime(), new EphemeralDataProtectionProvider());
        var tokens = new TokenStore(storage);
        using var identity = new JwtAuthStateProvider(tokens);
        var jwt = new JwtSecurityToken(notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(10));
        await tokens.SetTokenAsync(new JwtSecurityTokenHandler().WriteToken(jwt));
        var notifications = 0;
        identity.AuthenticationStateChanged += _ => notifications++;
        using var http = new HttpClient(new StatusHandler(status)) { BaseAddress = new Uri("https://test.invalid") };
        var api = new FaunexApiClient(new ClientFactory(http), tokens, new TestNavigation());

        await Assert.ThrowsAsync<InvalidOperationException>(() => api.GetAsync<object>("/api/auth/me"));
        Assert.Equal(authenticated, (await identity.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.Equal(authenticated ? 0 : 1, notifications);
    }

    [Fact]
    public async Task Expired_browser_token_is_cleared_and_identity_notified()
    {
        var tokens = new TokenStore(new ProtectedLocalStorage(new MemoryJsRuntime(), new EphemeralDataProtectionProvider()));
        using var identity = new JwtAuthStateProvider(tokens);
        var jwt = new JwtSecurityToken(notBefore: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1));
        await tokens.SetTokenAsync(new JwtSecurityTokenHandler().WriteToken(jwt));
        Assert.False((await identity.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.Null(await tokens.GetTokenAsync());
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("") });
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class TestNavigation : Microsoft.AspNetCore.Components.NavigationManager
    {
        public TestNavigation() => Initialize("https://faunex.co.za/", "https://faunex.co.za/");
    }

    private sealed class MemoryJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string> _values = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            var key = (string)args![0]!;
            if (identifier == "localStorage.setItem") _values[key] = (string)args[1]!;
            if (identifier == "localStorage.removeItem") _values.Remove(key);
            return ValueTask.FromResult(identifier == "localStorage.getItem" && _values.TryGetValue(key, out var value)
                ? (TValue)(object)value : default!);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
