using Faunex.Application.DTOs;
using Faunex.Web.Components;
using Faunex.Web.Payments;
using Faunex.Web.Api;
using Faunex.Web.Auth;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Faunex.Tests;

public sealed class PaymentPresentationTests
{
    [Theory]
    [InlineData("cancel", "Pending", "Checkout was cancelled in the browser", "Payment is not yet confirmed here")]
    [InlineData("return", "Pending", "Payment is not yet confirmed here", "Checkout was cancelled in the browser")]
    [InlineData("cancel", "SandboxComplete", "Do not pay this order again", "Checkout was cancelled in the browser")]
    [InlineData("return", "SandboxComplete", "PayFast sandbox payment is confirmed", "Payment is not yet confirmed here")]
    [InlineData("cancel", "ReviewRequired", "needs tenant review", "Checkout was cancelled in the browser")]
    [InlineData("return", "UnexpectedState", "needs tenant review", "Payment is not yet confirmed here")]
    public async Task Verified_state_takes_priority_over_untrusted_return_query(string returned, string status, string expected, string absent)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<PayFastReturnNotice>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(PayFastReturnNotice.PaymentStatus)] = status, [nameof(PayFastReturnNotice.ReturnState)] = returned }));
            return result.ToHtmlString();
        });
        Assert.Contains(expected, html); Assert.DoesNotContain(absent, html);
        Assert.DoesNotContain("<form", html); Assert.DoesNotContain("<script", html);
    }

    [Theory]
    [InlineData(null, "pricing", "Pricing needed")]
    [InlineData("Pending", "pending", "Awaiting confirmation")]
    [InlineData("SandboxComplete", "confirmed", "Sandbox confirmed")]
    [InlineData("ReviewRequired", "review", "Payment review required")]
    [InlineData("UnexpectedState", "review", "Payment review required")]
    public void Order_stages_and_labels_fail_closed_for_unknown_payment_state(string? paymentStatus, string stage, string label)
    {
        var summary = paymentStatus is null ? null : new PayFastSummaryDto(Guid.NewGuid(), "Sandbox", paymentStatus, 100, "ZAR", "10000100", null, null, []);
        var order = new AuctionOrderDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TEST ONLY", 1, 100, "ZAR", DateTimeOffset.UtcNow, Payment: summary);
        Assert.Equal(stage, order.PaymentStage); Assert.Equal(label, OrderPaymentDisplay.Label(stage));
    }

    [Theory]
    [InlineData("Pending", true, false, true)]
    [InlineData("Pending", false, false, false)]
    [InlineData("Pending", true, true, true)]
    [InlineData("SandboxComplete", true, false, false)]
    [InlineData("ReviewRequired", true, false, false)]
    public async Task Payment_component_checks_current_state_and_never_offers_unacknowledged_resume(string status, bool buyer, bool unavailable, bool resumeVisible)
    {
        var summary = new PayFastSummaryDto(Guid.NewGuid(), "Sandbox", status, 100, "ZAR", "10000100", null, null, []);
        var order = new AuctionOrderDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TEST ONLY", 1, 100, "ZAR", DateTimeOffset.UtcNow,
            CheckoutAvailable: status == "Pending", Payment: summary);
        var handlers = new PaymentViewHandler(order, unavailable);
        var services = new ServiceCollection();
        services.AddLogging(); services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<NavigationManager>(new PaymentNavigation()); services.AddSingleton<IJSRuntime, NoBrowserStorage>();
        services.AddScoped<ProtectedLocalStorage>(); services.AddScoped<TokenStore>(); services.AddScoped<FaunexApiClient>();
        services.AddHttpClient("FaunexApi", x => x.BaseAddress = new Uri("https://isolated.example.test")).ConfigurePrimaryHttpMessageHandler(() => handlers);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<PayFastPayment>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(PayFastPayment.OrderId)] = order.Id, [nameof(PayFastPayment.Summary)] = summary,
                [nameof(PayFastPayment.CanPay)] = buyer, [nameof(PayFastPayment.CanPrepare)] = true,
                [nameof(PayFastPayment.ReturnState)] = "cancel"
            }));
            return result.ToHtmlString();
        });
        Assert.Equal(resumeVisible, html.Contains("Resume Sandbox Checkout"));
        if (resumeVisible) Assert.Matches("<button[^>]*disabled[^>]*>Resume Sandbox Checkout</button>", html);
        Assert.DoesNotContain("<form", html);
        Assert.Equal(2, handlers.Reads);
        if (unavailable) Assert.Contains("Isolated status unavailable", html);
        else if (status == "Pending") Assert.Contains("Checkout was cancelled in the browser", html);
        else if (status == "SandboxComplete") Assert.Contains("PayFast sandbox payment is confirmed", html);
        else Assert.Contains("needs tenant review", html);
    }

    private sealed class PaymentViewHandler(AuctionOrderDto order, bool unavailable) : HttpMessageHandler
    {
        public int Reads;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method); Reads++;
            return Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/payment", StringComparison.Ordinal)
                ? unavailable ? new(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { error = "Isolated status unavailable" }) }
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(order.Payment) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(order) });
        }
    }
    private sealed class PaymentNavigation : NavigationManager
    {
        public PaymentNavigation() => Initialize("https://isolated.example.test/", "https://isolated.example.test/buyer/orders");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }
    private sealed class NoBrowserStorage : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Static renderer has no browser storage.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
