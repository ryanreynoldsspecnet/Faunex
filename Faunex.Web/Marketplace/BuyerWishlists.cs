using Faunex.Application.DTOs;
using Faunex.Web.Api;
using Microsoft.AspNetCore.Components.Authorization;

namespace Faunex.Web.Marketplace;

public sealed class BuyerWishlists(FaunexApiClient api, AuthenticationStateProvider auth) : IDisposable
{
    public BuyerWishlistStateDto? State { get; private set; }
    public bool Busy { get; private set; }
    public string? Notice { get; private set; }
    public Guid? PickedListing { get; private set; }
    public string PickedTitle { get; private set; } = "";
    public event Action? Changed;
    private Task? _load;
    private int _generation;
    private int _readVersion;
    private bool _subscribed;
    public bool Contains(Guid id) => State?.Lists.Any(x => x.ListingIds.Contains(id)) == true;
    public async Task EnsureAsync()
    {
        if (!_subscribed) { auth.AuthenticationStateChanged += AuthenticationChanged; _subscribed = true; }
        var user = (await auth.GetAuthenticationStateAsync()).User;
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Buyer")) throw new InvalidOperationException("Sign in with a buyer account to use wishlists.");
        if (State is not null) return;
        if (_load is null) _load = LoadAsync(_generation);
        await _load;
        if (State is null) throw new InvalidOperationException("Your wishlist session changed. Please refresh it.");
    }
    private async Task LoadAsync(int generation)
    {
        var version = ++_readVersion;
        var state = await api.GetAsync<BuyerWishlistStateDto>("/api/buyer/wishlists") ?? throw new InvalidOperationException("No wishlist response.");
        if (state.Lists is null) throw new InvalidOperationException("Wishlists could not be loaded. Please refresh them.");
        if (generation == _generation && version == _readVersion) { State = state; Changed?.Invoke(); }
    }
    public async Task RefreshAsync()
    {
        var generation = _generation;
        _load = null;
        try { await EnsureAsync(); await LoadAsync(generation); }
        catch { if (generation == _generation) { State = null; _load = null; ++_readVersion; Changed?.Invoke(); } throw; }
    }
    public void Open(Guid listing, string title) { PickedListing = listing; PickedTitle = title; Changed?.Invoke(); }
    public void Close() { PickedListing = null; PickedTitle = ""; Changed?.Invoke(); }
    public void DismissNotice() { Notice = null; Changed?.Invoke(); }
    public async Task<Guid> CreateAsync(string name)
    {
        await EnsureAsync(); var before = State!.Lists.Select(x => x.Id).ToHashSet();
        await ChangeAsync(() => api.PostAsync<WishlistNameRequest, BuyerWishlistStateDto>("/api/buyer/wishlists", new(State!.Revision, name)), "Wishlist created.");
        return State!.Lists.Single(x => !before.Contains(x.Id)).Id;
    }
    public Task RenameAsync(Guid id, string name) => ChangeAsync(() => api.PutAsync<WishlistNameRequest, BuyerWishlistStateDto>($"/api/buyer/wishlists/{id}", new(State!.Revision, name)), "Wishlist renamed.");
    public Task DeleteAsync(Guid id) => ChangeAsync(() => api.DeleteFileAsync<BuyerWishlistStateDto>($"/api/buyer/wishlists/{id}?revision={State!.Revision}"), "Wishlist deleted.");
    public Task ClearUnavailableAsync(Guid id) => ChangeAsync(() => api.DeleteFileAsync<BuyerWishlistStateDto>($"/api/buyer/wishlists/{id}/unavailable?revision={State!.Revision}"), "Unavailable entries removed.");
    public Task SaveAsync(Guid listing, IReadOnlyList<Guid> lists) => ChangeAsync(() => api.PutAsync<WishlistMembershipRequest, BuyerWishlistStateDto>($"/api/buyer/wishlists/items/{listing}", new(State!.Revision, lists)), lists.Count == 0 ? "Removed from your wishlists." : $"Saved to {lists.Count} {(lists.Count == 1 ? "wishlist" : "wishlists")}.");
    private async Task ChangeAsync(Func<Task<BuyerWishlistStateDto?>> change, string notice)
    {
        if (Busy) throw new InvalidOperationException("Please wait for the current wishlist change.");
        Busy = true; Notice = null; ++_readVersion; var generation = _generation; Changed?.Invoke();
        try
        {
            await EnsureAsync();
            if (generation != _generation || State is null) throw new InvalidOperationException("Your session changed. Reload your wishlists.");
            var state = await change() ?? throw new InvalidOperationException("The change could not be confirmed. Refresh your wishlists before trying again.");
            if (generation == _generation) { ++_readVersion; State = state; Notice = notice; }
        }
        catch { if (generation == _generation) { State = null; _load = null; ++_readVersion; } throw; }
        finally { Busy = false; Changed?.Invoke(); }
    }
    private void AuthenticationChanged(Task<AuthenticationState> _) { ++_generation; ++_readVersion; State = null; Notice = null; _load = null; Close(); }
    public void Dispose() { ++_generation; if (_subscribed) auth.AuthenticationStateChanged -= AuthenticationChanged; }
}
