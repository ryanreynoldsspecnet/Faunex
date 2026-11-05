using System.Net;
using System.Net.Http.Json;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;

namespace Faunex.Tests;

public sealed class OrderPaymentWorkspaceTests
{
    private static readonly string[] Stages = ["pricing", "unpaid", "pending", "confirmed", "review"];

    private static async Task AssertStage(PayFastCase test, string expected)
    {
        foreach (var client in new[] { test.Buyer, test.Auction.Admin })
        foreach (var stage in Stages)
        {
            var page = (await client.GetFromJsonAsync<PagedResult<AuctionOrderDto>>($"/api/orders?paymentStage={stage}&take=1"))!;
            Assert.Equal(stage == expected ? 1 : 0, page.Total);
            if (stage == expected)
            {
                var row = Assert.Single(page.Items);
                Assert.Equal(test.Order.Id, row.Id); Assert.Equal(expected, row.PaymentStage);
                var next = (await client.GetFromJsonAsync<PagedResult<AuctionOrderDto>>($"/api/orders?paymentStage={stage}&skip=1&take=1"))!;
                Assert.Equal(1, next.Total); Assert.Empty(next.Items);
            }
            else Assert.Empty(page.Items);
        }
    }

    [Theory]
    [InlineData(false, "pricing")]
    [InlineData(true, "unpaid")]
    public async Task Order_filters_match_pricing_and_unstarted_checkout(bool withQuote, string stage)
    {
        await using var test = await PayFastCase.CreateAsync(withQuote: withQuote);
        await AssertStage(test, stage);
        var all = (await test.Buyer.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders?paymentStage="))!;
        Assert.Equal(1, all.Total);
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Buyer.GetAsync("/api/orders?paymentStage=paid")).StatusCode);
        var empty = (await test.Buyer.GetFromJsonAsync<PagedResult<AuctionOrderDto>>($"/api/orders?paymentStage={stage}&search=absent-listing-title"))!;
        Assert.Equal(0, empty.Total); Assert.Empty(empty.Items);
    }

    [Fact]
    public async Task Cancel_return_resume_late_completion_and_conflict_keep_order_stage_authoritative()
    {
        await using var test = await PayFastCase.CreateAsync(); var checkout = await test.Prepare();
        await AssertStage(test, "pending");
        var original = (await test.Buyer.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{test.Order.Id}"))!;
        var returned = (await test.Buyer.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{test.Order.Id}?payment=cancel&paid=true"))!;
        Assert.Equal("pending", returned.PaymentStage); Assert.Null(returned.Payment!.ConfirmedAt);
        Assert.Equal(checkout.AttemptId, (await test.Prepare()).AttemptId);
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout, status: "CANCELLED"))).StatusCode);
        await AssertStage(test, "pending");
        Assert.Equal(checkout.AttemptId, (await test.Prepare()).AttemptId);
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout))).StatusCode);
        await AssertStage(test, "confirmed");
        var paid = (await test.Buyer.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{test.Order.Id}?payment=cancel"))!;
        Assert.Equal("confirmed", paid.PaymentStage); Assert.False(paid.CheckoutAvailable);
        Assert.Equal(original.Quote!.Id, paid.Quote!.Id); Assert.Equal(original.Quote.Pricing.Total, paid.Quote.Pricing.Total);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Buyer.PostAsJsonAsync(test.CheckoutPath, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout))).StatusCode);
        await AssertStage(test, "confirmed");
        Assert.Equal(HttpStatusCode.OK, (await test.Notify(test.Body(checkout, provider: "54321"))).StatusCode);
        await AssertStage(test, "review");
        Assert.Equal(HttpStatusCode.Conflict, (await test.Buyer.PostAsJsonAsync(test.CheckoutPath, new { })).StatusCode);
        var history = (await test.Auction.Admin.GetFromJsonAsync<PayFastSummaryDto>($"/api/orders/{test.Order.Id}/payment"))!;
        Assert.Equal(3, history.Events.Count); Assert.Contains(history.Events, x => x.Decision == "ReviewRequired");
    }

    [Fact]
    public async Task Every_filtered_count_and_page_preserves_buyer_and_tenant_isolation()
    {
        await using var test = await PayFastCase.CreateAsync(); await test.Prepare();
        using var otherBuyer = await test.Auction.BuyerAsync();
        var otherTenant = await test.App.AddTenantAsync();
        using var foreignAdmin = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(otherTenant, FaunexRoles.TenantAdmin)));
        foreach (var client in new[] { otherBuyer, foreignAdmin })
        foreach (var stage in Stages)
        {
            var page = (await client.GetFromJsonAsync<PagedResult<AuctionOrderDto>>($"/api/orders?paymentStage={stage}"))!;
            Assert.Equal(0, page.Total); Assert.Empty(page.Items);
        }
    }

    [PostgresFact]
    public Task Payment_stage_queries_translate_on_production_database() => ProductionFilters();

    private static async Task ProductionFilters()
    {
        await using var test = await PayFastCase.CreateAsync();
        await AssertStage(test, "unpaid"); await test.Prepare(); await AssertStage(test, "pending");
    }
}
