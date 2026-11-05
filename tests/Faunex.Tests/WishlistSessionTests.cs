using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Faunex.Application.DTOs;
using Faunex.Web.Api;
using Faunex.Web.Auth;
using Faunex.Web.Marketplace;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Faunex.Tests;

public sealed class WishlistSessionTests
{
    [Fact]
    public async Task Many_lot_hearts_share_one_metadata_request()
    {
        var handler = new SessionHandler(); await using var provider = Services(handler);
        var lists = provider.GetRequiredService<BuyerWishlists>();
        await Task.WhenAll(Enumerable.Range(0, 48).Select(_ => lists.EnsureAsync()));
        Assert.Equal(1, handler.Reads); Assert.NotNull(lists.State);
    }
    [Fact]
    public async Task Failed_mutation_clears_cached_revision_and_never_shows_success_or_retries_post()
    {
        var handler = new SessionHandler { FailWrite = true }; await using var provider = Services(handler);
        var lists = provider.GetRequiredService<BuyerWishlists>(); await lists.EnsureAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => lists.CreateAsync("TEST ONLY"));
        Assert.Null(lists.State); Assert.Null(lists.Notice); Assert.Equal(1, handler.Writes); Assert.False(lists.Busy);
        await lists.RefreshAsync(); Assert.NotNull(lists.State); Assert.Equal(1, handler.Writes);
    }
    [Fact]
    public async Task Failed_metadata_load_is_shared_until_an_explicit_refresh()
    {
        var handler = new SessionHandler { FailRead = true }; await using var provider = Services(handler);
        var lists = provider.GetRequiredService<BuyerWishlists>();
        await Task.WhenAll(Enumerable.Range(0, 48).Select(async _ => await Assert.ThrowsAsync<InvalidOperationException>(() => lists.EnsureAsync())));
        Assert.Equal(1, handler.Reads); Assert.Null(lists.State);
        handler.FailRead = false; await lists.RefreshAsync(); Assert.NotNull(lists.State);
    }
    [Fact]
    public async Task Identity_change_clears_private_state_picker_and_success_message()
    {
        var handler = new SessionHandler(); await using var provider = Services(handler);
        var lists = provider.GetRequiredService<BuyerWishlists>(); var auth = (SessionAuth)provider.GetRequiredService<AuthenticationStateProvider>();
        await lists.CreateAsync("TEST ONLY"); lists.Open(Guid.NewGuid(), "Private title"); Assert.NotNull(lists.Notice);
        auth.Change(); Assert.Null(lists.State); Assert.Null(lists.PickedListing); Assert.Empty(lists.PickedTitle); Assert.Null(lists.Notice);
        handler.Current = new(Guid.Empty, []); await lists.EnsureAsync(); Assert.Empty(lists.State!.Lists);
    }
    [Fact]
    public async Task Slow_read_from_previous_identity_cannot_restore_private_state()
    {
        var handler = new SessionHandler { HoldNextRead = true, Current = new(Guid.NewGuid(), [new(Guid.NewGuid(), "Previous buyer private name", [], 0)]) };
        await using var provider = Services(handler); var lists = provider.GetRequiredService<BuyerWishlists>();
        var load = lists.EnsureAsync(); await handler.Started.Task;
        ((SessionAuth)provider.GetRequiredService<AuthenticationStateProvider>()).Change(); handler.Release.TrySetResult(); await Assert.ThrowsAsync<InvalidOperationException>(() => load);
        Assert.Null(lists.State); handler.Current = new(Guid.Empty, []); await lists.EnsureAsync(); Assert.Empty(lists.State!.Lists);
    }
    [Fact]
    public async Task Slow_refresh_cannot_replace_a_confirmed_mutation_with_old_data()
    {
        var handler = new SessionHandler(); await using var provider = Services(handler); var lists = provider.GetRequiredService<BuyerWishlists>();
        await lists.EnsureAsync(); handler.HoldNextRead = true; var refresh = lists.RefreshAsync(); await handler.Started.Task;
        await lists.CreateAsync("New list"); var revision = lists.State!.Revision; handler.Release.TrySetResult(); await refresh;
        Assert.Equal(revision, lists.State!.Revision); Assert.Equal("New list", lists.State.Lists.Single().Name);
    }
    [Fact]
    public void Wishlist_login_continuation_is_a_specific_safe_route_not_a_redirect_prefix()
    {
        Assert.Equal("/buyer/wishlists", BuyerReturnRoute.Validate("/buyer/wishlists"));
        Assert.Null(BuyerReturnRoute.Validate("/buyer/wishlists/../../platform"));
        Assert.Null(BuyerReturnRoute.Validate("/buyer/wishlists?returnTo=https://evil.example"));
    }
    private static ServiceProvider Services(SessionHandler handler)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<NavigationManager>(new SessionNavigation()); services.AddSingleton<IJSRuntime, NoStorage>();
        services.AddSingleton<AuthenticationStateProvider, SessionAuth>(); services.AddScoped<ProtectedLocalStorage>(); services.AddScoped<TokenStore>(); services.AddScoped<FaunexApiClient>(); services.AddScoped<BuyerWishlists>();
        services.AddHttpClient("FaunexApi", c => c.BaseAddress = new Uri("https://isolated.example.test")).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }
    private sealed class SessionAuth : AuthenticationStateProvider
    {
        private Guid _user = Guid.NewGuid();
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, _user.ToString()), new(ClaimTypes.Role, "Buyer")], "test"))));
        public void Change() { _user = Guid.NewGuid(); NotifyAuthenticationStateChanged(GetAuthenticationStateAsync()); }
    }
    private sealed class SessionNavigation : NavigationManager
    {
        public SessionNavigation() => Initialize("https://isolated.example.test/", "https://isolated.example.test/browse");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }
    private sealed class NoStorage : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("No browser storage in unit test.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class SessionHandler : HttpMessageHandler
    {
        public BuyerWishlistStateDto Current = new(Guid.Empty, []);
        public int Reads, Writes;
        public bool FailWrite, FailRead, HoldNextRead;
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Get)
            {
                Reads++; var state = Current;
                if (FailRead) return new(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "Isolated metadata unavailable" }) };
                if (HoldNextRead) { HoldNextRead = false; Started.TrySetResult(); await Release.Task.WaitAsync(ct); }
                else await Task.Delay(10, ct);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(state) };
            }
            Writes++;
            if (FailWrite) return new(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "Isolated wishlist write unavailable" }) };
            var body = await request.Content!.ReadFromJsonAsync<WishlistNameRequest>(cancellationToken: ct);
            Current = new(Guid.NewGuid(), [new(Guid.NewGuid(), body!.Name, [], 0)]);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Current) };
        }
    }
}
