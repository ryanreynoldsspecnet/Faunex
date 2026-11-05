using System.Net;
using System.Net.Http.Json;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Application.Tenancy;
using Faunex.Domain.Entities;

namespace Faunex.Tests;

public sealed class BuyerBidActivityTests
{
    private const string Path = "/api/buyer/bids";

    [Fact]
    public async Task History_requires_a_buyer_and_rejects_unknown_filters()
    {
        await using var test = await AuctionCase.CreateAsync();
        using var anonymous = await test.Files.App.Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var admin = await test.Admin.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        using var buyer = await test.BuyerAsync();
        using var invalid = await buyer.GetAsync(Path + "?status=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Empty((await Read(buyer)).Items);
    }

    [PostgresFact]
    public async Task History_tracks_own_bids_and_authoritative_results_without_exposing_competitor_or_withdrawn_details()
    {
        await using var test = await AuctionCase.CreateAsync();
        await test.Publish();
        test.Clock.Set(test.Starts);
        using var buyer = await test.BuyerAsync();
        using var rival = await test.BuyerAsync();
        using var first = await test.Bid(buyer, 100m);
        first.EnsureSuccessStatusCode();
        Assert.Equal("Leading", Assert.Single((await Read(buyer, "?status=progress")).Items).Status);
        using var competing = await test.Bid(rival, 110m);
        competing.EnsureSuccessStatusCode();
        var outbid = Assert.Single((await Read(buyer)).Items);
        Assert.Equal("Outbid", outbid.Status);
        Assert.Equal(100m, outbid.Amount);
        Assert.Equal(test.Id, outbid.ListingId);
        Assert.True(outbid.AuctionAvailable);
        Assert.Equal((await test.Snapshot()).EndsAt, outbid.EndsAt);
        Assert.Null(outbid.OrderId);
        using var last = await test.Bid(buyer, 120m);
        last.EnsureSuccessStatusCode();
        var latest = await Read(buyer, "?take=1");
        Assert.Equal(2, latest.Total);
        Assert.Equal(120m, Assert.Single(latest.Items).Amount);
        Assert.Equal(100m, Assert.Single((await Read(buyer, "?skip=1&take=1")).Items).Amount);
        Assert.Empty((await Read(buyer, "?status=results")).Items);

        var foreignTenant = await test.Files.App.AddTenantAsync();
        using var foreign = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(
            await test.Files.App.AddUserAsync(foreignTenant, FaunexRoles.Buyer)));
        Assert.Empty((await Read(foreign, $"?tenantId={test.Files.Tenant}&listingId={test.Id}")).Items);
        buyer.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader);
        buyer.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(foreignTenant));
        using var wrongHost = await buyer.GetAsync(Path);
        Assert.False(wrongHost.IsSuccessStatusCode);
        buyer.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader);
        buyer.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(test.Files.Tenant));

        test.Clock.Set(test.Ends);
        Assert.All((await Read(buyer, "?status=progress")).Items, row => Assert.Equal("AwaitingResult", row.Status));
        var terms = await test.Snapshot();
        using var closed = await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { terms.Revision });
        closed.EnsureSuccessStatusCode();
        Assert.Empty((await Read(buyer, "?status=progress")).Items);
        var won = (await Read(buyer, "?status=results")).Items;
        Assert.Equal(2, won.Count);
        Assert.All(won, row => Assert.Equal("Won", row.Status));
        Assert.True(won[0].IsWinningBid);
        Assert.False(won[1].IsWinningBid);
        Assert.NotNull(won[0].OrderId);
        Assert.Equal(won[0].OrderId, won[1].OrderId);
        var lost = Assert.Single((await Read(rival, "?status=results")).Items);
        Assert.Equal("NotWon", lost.Status);
        Assert.Equal(110m, lost.Amount);
        Assert.Null(lost.OrderId);
        Assert.False(lost.IsWinningBid);

        await test.Files.SetState(ListingComplianceStatus.Suspended, false);
        var hidden = Assert.Single((await Read(rival)).Items);
        Assert.Null(hidden.Title);
        Assert.False(hidden.AuctionAvailable);
        Assert.Equal("NotWon", hidden.Status);
        Assert.All((await Read(buyer)).Items, row =>
        {
            Assert.False(row.AuctionAvailable);
            Assert.Equal(won[0].OrderId, row.OrderId);
        });
    }

    [PostgresFact]
    public async Task Reserve_not_met_is_a_result_without_a_winner_order()
    {
        await using var test = await AuctionCase.CreateAsync(150m);
        await test.Publish();
        test.Clock.Set(test.Starts);
        using var buyer = await test.BuyerAsync();
        using var bid = await test.Bid(buyer, 100m);
        bid.EnsureSuccessStatusCode();
        test.Clock.Set(test.Ends);
        var terms = await test.Snapshot();
        using var close = await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { terms.Revision });
        close.EnsureSuccessStatusCode();
        var row = Assert.Single((await Read(buyer, "?status=results")).Items);
        Assert.Equal("ReserveNotMet", row.Status);
        Assert.False(row.IsWinningBid);
        Assert.Null(row.OrderId);
    }

    private static async Task<PagedResult<BuyerBidActivityDto>> Read(HttpClient client, string query = "")
    {
        using var response = await client.GetAsync(Path + query);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await response.Content.ReadAsStringAsync();
        foreach (var key in new[] { "bidderId", "leader", "winnerId", "reservePrice", "winningBidId", "chargeTermsJson" })
            Assert.DoesNotContain($"\"{key}\"", json, StringComparison.OrdinalIgnoreCase);
        return (await response.Content.ReadFromJsonAsync<PagedResult<BuyerBidActivityDto>>())!;
    }
}
