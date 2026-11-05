using Faunex.Application.DTOs;
using Faunex.Web.Api;
using Faunex.Web.Auth;
using Faunex.Web.Components.Layout;
using Faunex.Web.Components.Pages;
using Faunex.Web.Marketplace;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Faunex.Tests;

public sealed class AuctionStorefrontTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static PublicAuctionDto Live => new(Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(-1), Now.AddHours(1), 100m, 10m, null, "Live", false, null, Now);

    [Fact]
    public void Review_preserves_exact_amount_and_does_not_treat_it_as_proxy_bidding()
    {
        var auction = Live;
        var review = AuctionBidReview.Prepare(auction, 175.25m, Now);
        Assert.Equal(175.25m, review.Amount); Assert.Equal(auction.Id, review.AuctionId);
        Assert.True(review.Matches(auction with { ServerTime = Now.AddSeconds(5) }, Now.AddSeconds(5)));
        Assert.Equal(100m, AuctionBidReview.Minimum(auction));
        Assert.Equal(130m, AuctionBidReview.Minimum(auction with { CurrentPrice = 120m }));
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(99)][InlineData(100.001)][InlineData(1000000000)]
    public void Review_rejects_invalid_amount(decimal amount) => Assert.Throws<InvalidOperationException>(() => AuctionBidReview.Prepare(Live, amount, Now));

    [Theory]
    [InlineData("Scheduled")][InlineData("AwaitingResult")][InlineData("Closed")][InlineData("Cancelled")][InlineData("Unexpected")]
    public void Review_rejects_non_live_state(string state) => Assert.Throws<InvalidOperationException>(() => AuctionBidReview.Prepare(Live with { State = state }, 100m, Now));

    [Fact]
    public void Review_expires_on_competitor_bid_rules_dates_ids_or_close_even_if_amount_is_still_high_enough()
    {
        var auction = Live; var review = AuctionBidReview.Prepare(auction, 500m, Now);
        foreach (var changed in new[] { auction with { CurrentPrice = 110m }, auction with { MinimumIncrement = 20m },
            auction with { StartingPrice = 150m }, auction with { StartsAt = Now.AddHours(-2) }, auction with { EndsAt = Now.AddHours(2) },
            auction with { Id = Guid.NewGuid() }, auction with { ListingId = Guid.NewGuid() }, auction with { IsClosed = true }, auction with { State = "Closed" } })
            Assert.False(review.Matches(changed, Now));
        Assert.False(review.Matches(auction, auction.EndsAt!.Value));
        Assert.False(AuctionBidReview.IsOpen(auction, auction.StartsAt!.Value.AddTicks(-1)));
        Assert.False(AuctionBidReview.IsOpen(auction with { EndsAt = null }, Now));
    }

    [Theory]
    [InlineData("https://evil.example/auctions/00000000-0000-0000-0000-000000000000")]
    [InlineData("//evil.example")][InlineData("/platform")][InlineData("/buyer/orders")]
    [InlineData("/auctions/../platform")][InlineData("/auctions/not-a-guid")]
    [InlineData("/auctions/00000000-0000-0000-0000-000000000000?returnTo=https://evil.example")]
    [InlineData("/listings/00000000-0000-0000-0000-000000000000#fragment")]
    public void Buyer_return_route_rejects_external_or_unexpected_destinations(string route) => Assert.Null(BuyerReturnRoute.Validate(route));

    [Fact]
    public void Buyer_return_route_accepts_only_a_normalised_lot_or_auction()
    {
        var id = Guid.NewGuid();
        Assert.Equal($"/auctions/{id}", BuyerReturnRoute.Validate($"/auctions/{id.ToString().ToUpperInvariant()}"));
        Assert.Equal($"/listings/{id}", BuyerReturnRoute.Validate($"/listings/{id}"));
        Assert.Null(BuyerReturnRoute.Validate(null));
    }

    [Theory]
    [InlineData("Live", null, false, "Log in to bid")]
    [InlineData("Live", null, true, "Review bid")]
    [InlineData("Scheduled", null, true, "Bidding opens at the time shown above")]
    [InlineData("AwaitingResult", null, true, "final result is being confirmed")]
    [InlineData("Closed", "Sold", true, "Check my winner order")]
    [InlineData("Closed", "ReserveNotMet", true, "This lot was not sold")]
    [InlineData("Closed", "NoBids", false, "without an accepted bid")]
    [InlineData("Cancelled", null, true, "No further bids can be placed")]
    public async Task Auction_page_renders_actual_states_roles_and_recorded_charges(string state, string? outcome, bool buyer, string expected)
    {
        var handler = new AuctionHandler(state, outcome);
        await using var provider = Services(handler, buyer);
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AuctionDetails>(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(AuctionDetails.ListingId)] = handler.Auction.ListingId }))).ToHtmlString());
        Assert.Contains(expected, html); Assert.Contains("Description for TEST ONLY", html);
        Assert.Contains("Delivery &amp; additional charges", html); Assert.Contains("Choose one at checkout", html);
        Assert.Contains("Required per lot", html); Assert.Contains("Optional per lot", html);
        Assert.Contains("Collection", html); Assert.Contains("Carrier charge", html); Assert.Contains("Optional crate", html);
        Assert.Contains("SAST", html); Assert.DoesNotContain("Confirm bid", html); Assert.DoesNotContain("Place Bid", html);
        if (state != "Live" || !buyer) Assert.DoesNotContain("id=\"bid-amount\"", html);
        if (state != "Live") Assert.DoesNotContain("Minimum next bid", html);
        if (!buyer && state == "Live") Assert.Contains("returnTo=%2Fauctions%2F", html);
        Assert.Equal(0, handler.Writes); Assert.Equal(1, handler.PhotoReads);
    }

    [Fact]
    public async Task Auction_update_failure_keeps_bidding_unavailable_and_does_not_expose_internal_error()
    {
        var handler = new AuctionHandler("Live", null) { Unavailable = true };
        await using var provider = Services(handler, true);
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AuctionDetails>(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(AuctionDetails.ListingId)] = handler.Auction.ListingId }))).ToHtmlString());
        Assert.Contains("Bidding is paused", html); Assert.DoesNotContain("id=\"bid-amount\"", html);
        Assert.DoesNotContain("isolated internal failure", html); Assert.Equal(0, handler.Writes);
    }

    [Fact]
    public async Task Parent_refresh_does_not_refetch_photos_but_changing_lot_does()
    {
        var handler = new AuctionHandler("Live", null);
        await using var provider = Services(handler, false);
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        GalleryHarness? harness = null;
        await renderer.Dispatcher.InvokeAsync(async () => await renderer.RenderComponentAsync<GalleryHarness>(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(GalleryHarness.Capture)] = (Action<GalleryHarness>)(x => harness = x) })));
        Assert.Equal(1, handler.PhotoReads);
        await renderer.Dispatcher.InvokeAsync(() => harness!.Refresh(false));
        Assert.Equal(1, handler.PhotoReads);
        await renderer.Dispatcher.InvokeAsync(() => harness!.Refresh(true));
        Assert.Equal(2, handler.PhotoReads);
    }

    public sealed class GalleryHarness : ComponentBase
    {
        [Parameter] public Action<GalleryHarness>? Capture { get; set; }
        private Guid _id = Guid.NewGuid();
        protected override void OnInitialized() => Capture?.Invoke(this);
        public void Refresh(bool changeLot) { if (changeLot) _id = Guid.NewGuid(); StateHasChanged(); }
        protected override void BuildRenderTree(RenderTreeBuilder builder) { builder.OpenComponent<ListingPhotoGallery>(0); builder.AddAttribute(1, "ListingId", _id); builder.CloseComponent(); }
    }
    private static ServiceProvider Services(AuctionHandler handler, bool buyer)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<NavigationManager>(new AuctionNavigation()); services.AddSingleton<IJSRuntime, NoStorage>();
        services.AddSingleton<AuthenticationStateProvider>(new AuctionAuth(buyer));
        services.AddScoped<ProtectedLocalStorage>(); services.AddScoped<TokenStore>(); services.AddScoped<FaunexApiClient>();
        services.AddHttpClient("FaunexApi", x => x.BaseAddress = new Uri("https://isolated.example.test")).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }
    private sealed class AuctionNavigation : NavigationManager
    {
        public AuctionNavigation() => Initialize("https://isolated.example.test/", "https://isolated.example.test/auctions");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }
    private sealed class AuctionAuth(bool buyer) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(buyer
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Buyer")], "test"))
            : new ClaimsPrincipal(new ClaimsIdentity())));
    }
    private sealed class NoStorage : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("No browser storage in static render.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class AuctionHandler(string state, string? outcome) : HttpMessageHandler
    {
        public PublicAuctionDto Auction { get; } = Live with { State = state, Outcome = outcome, IsClosed = state is "Closed" or "Cancelled",
            Charges = new(Guid.NewGuid(), "ZAR", "Recorded auction terms", [new(Guid.NewGuid(), "Collection", "Delivery", 0),
                new(Guid.NewGuid(), "Carrier charge", "RequiredExtra", 25), new(Guid.NewGuid(), "Optional crate", "OptionalExtra", 50)]) };
        public bool Unavailable { get; set; }
        public int Writes, PhotoReads;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Null(request.Headers.Authorization);
            if (request.Method != HttpMethod.Get) { Writes++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)); }
            object result;
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/photos")) { PhotoReads++; result = Array.Empty<Guid>(); }
            else if (path.EndsWith("/auctions"))
            {
                if (Unavailable) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "isolated internal failure" }) });
                result = new[] { Auction };
            }
            else if (path.EndsWith("/bids")) result = new PagedResult<PublicBidDto>([], 0);
            else result = new ListingDto(Auction.ListingId, Guid.NewGuid(), Guid.NewGuid(), "bird", null, "TEST ONLY lot", "Description for TEST ONLY", 100, null, "ZAR", 1, "Pretoria", true);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result, result.GetType()) });
        }
    }
}
