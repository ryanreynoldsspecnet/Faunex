using Faunex.Application.Tenancy;
using Faunex.Web.Api;
using Microsoft.AspNetCore.Components;

namespace Faunex.Web.Marketplace;

public sealed class MarketplaceBranding(IHttpClientFactory clients, NavigationManager navigation)
{
    private Task? _loading;
    public event Action? Changed;
    public bool IsPlatform => MarketplaceHosts.IsPlatform(navigation.ToAbsoluteUri(navigation.Uri).IdnHost);
    public MarketplaceContextDto? Context { get; private set; }
    public string Name => IsPlatform ? "Faunex" : Context?.MarketplaceDisplayName ?? Context?.CompanyName ?? Context?.Name ?? "Marketplace";
    public string? Logo => IsPlatform ? "/images/faunex-logo-text-transparent.png" : TenantBrandingRules.IsLogo(Context?.LogoUrl) ? Context?.LogoUrl : null;
    public string? Colour => TenantBrandingRules.IsColour(Context?.BrandPrimaryColor) ? Context!.BrandPrimaryColor : null;
    public Task EnsureLoadedAsync() => _loading ??= LoadAsync();
    public async Task RefreshAsync()
    {
        _loading = LoadAsync();
        await _loading;
        Changed?.Invoke();
    }
    private async Task LoadAsync()
    {
        if (!IsPlatform)
        {
            // Public branding must not depend on an expired or wrong-marketplace session.
            var host = navigation.ToAbsoluteUri(navigation.Uri).IdnHost;
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/marketplace/context?host={Uri.EscapeDataString(host)}");
            request.Headers.Add(MarketplaceHosts.OriginHeader, host);
            using var response = await clients.CreateClient("FaunexApi").SendAsync(request);
            response.EnsureSuccessStatusCode();
            Context = await response.Content.ReadFromJsonAsync<MarketplaceContextDto>();
        }
    }
}
