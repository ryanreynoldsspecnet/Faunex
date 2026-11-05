using Faunex.Application.DTOs;
using Faunex.Web.Components;
using Faunex.Web.Components.Layout;
using Faunex.Web.Marketplace;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Faunex.Tests;

public sealed class StorefrontPresentationTests
{
    [Theory]
    [InlineData("Live", null, "Bidding open")][InlineData("Scheduled", null, "Upcoming")]
    [InlineData("AwaitingResult", null, "Awaiting result")][InlineData("Closed", "Sold", "Sold")]
    [InlineData("Closed", "NoBids", "No bids")][InlineData("Cancelled", null, "Cancelled")]
    public async Task Lot_card_renders_real_state_price_photo_and_detail_link(string state, string? outcome, string label)
    {
        var now = DateTimeOffset.UtcNow;
        var lot = new MarketplaceLotDto(Guid.NewGuid(), "TEST ONLY - African grey", "bird", "Pretoria", 1, 100m, null, "ZAR", Guid.NewGuid(),
            new(Guid.NewGuid(), Guid.NewGuid(), now.AddHours(-1), now.AddHours(1), 100m, 10m, outcome == "Sold" ? 120m : null,
                state, state is "Closed" or "Cancelled", outcome, now));
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<MarketplaceLotCard>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(MarketplaceLotCard.Lot)] = lot }))).ToHtmlString());
        Assert.Contains(label, html); Assert.Contains($"/media/listing-photos/{lot.PhotoId}", html);
        Assert.Contains($"/auctions/{lot.Id}", html); Assert.Contains("SAST", html);
        Assert.Contains(outcome == "Sold" ? "Winning bid" : "Starting bid", html);
        Assert.DoesNotContain("Countdown", html); Assert.DoesNotContain("approved", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("#ffffff", "#111111")][InlineData("#000000", "#ffffff")][InlineData("#ffff00", "#111111")][InlineData(null, "#ffffff")]
    public void Tenant_brand_buttons_choose_readable_foreground(string? colour, string expected) => Assert.Equal(expected, StorefrontPresentation.BrandInk(colour));

    [Fact]
    public void Unpublished_and_fixed_lots_are_not_presented_as_live_auctions()
    {
        var lot = new MarketplaceLotDto(Guid.NewGuid(), "TEST ONLY", "unknown", null, 1, 100m, null, "ZAR", null, null);
        Assert.Equal("Catalogue lot", StorefrontPresentation.Status(lot));
        Assert.Equal("Fixed price", StorefrontPresentation.Status(lot with { FixedPrice = 150m }));
        Assert.Equal("Listed price", StorefrontPresentation.PriceLabel(lot with { FixedPrice = 150m }));
        Assert.Contains("USD", StorefrontPresentation.Money(100m, "USD"));
    }

    [Fact]
    public async Task Footer_waits_for_initial_tenant_branding_instead_of_staying_on_placeholder_name()
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<NavigationManager>(new StorefrontNavigation());
        services.AddHttpClient("FaunexApi", c => c.BaseAddress = new Uri("https://isolated.example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => new BrandingHandler());
        services.AddScoped<MarketplaceBranding>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StorefrontFooter>()).ToHtmlString());
        Assert.Contains("Stormbird", html); Assert.DoesNotContain(">Marketplace<", html);
        Assert.Contains("mailto:help@example.test", html);
    }
    private sealed class StorefrontNavigation : NavigationManager
    {
        public StorefrontNavigation() => Initialize("https://isolated.example.test/", "https://isolated.example.test/");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }
    private sealed class BrandingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new MarketplaceContextDto(Guid.NewGuid(), "Stormbird", null,
                "Stormbird", null, null, "#216449", null, null, "help@example.test", null, null, [])) });
    }
}
