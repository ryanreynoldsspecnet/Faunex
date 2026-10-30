using System.Net;
using System.Net.Http.Json;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class TimedAuctionTests
{
    [Fact]
    public async Task Draft_publication_bid_boundaries_minimums_and_private_public_contracts_are_enforced()
    {
        await using var test = await AuctionCase.CreateAsync(150m);
        Assert.Empty((await test.Files.App.Client.GetFromJsonAsync<List<PublicAuctionDto>>($"/api/marketplace/listings/{test.Id}/auctions{test.HostQuery}"))!);
        using var buyer = await test.BuyerAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await test.Bid(buyer, 100m)).StatusCode);
        await test.Publish();
        Assert.Equal(HttpStatusCode.Conflict, (await test.Bid(buyer, 100m)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { (await test.Snapshot()).Revision })).StatusCode);
        test.Clock.Set(test.Starts);
        foreach (var amount in new[] { 0m, -1m, 99.99m, 100.001m, 1000000000m })
            Assert.Equal(HttpStatusCode.BadRequest, (await test.Bid(buyer, amount)).StatusCode);
        Assert.True((await test.Bid(buyer, 100m)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Bid(buyer, 109.99m)).StatusCode);
        Assert.True((await test.Bid(buyer, 110m)).IsSuccessStatusCode);
        var publicResponse = await test.Files.App.Client.GetAsync($"/api/marketplace/listings/{test.Id}/auctions{test.HostQuery}");
        var json = await publicResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("reserve", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("winnerId", json, StringComparison.OrdinalIgnoreCase);
        var bids = await test.Files.App.Client.GetAsync($"/api/marketplace/auctions/{test.AuctionId}/bids{test.HostQuery}");
        Assert.DoesNotContain("bidderId", await bids.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync($"/api/auctions/{test.AuctionId}")).StatusCode);
        var current = await test.Snapshot();
        Assert.Equal(HttpStatusCode.Conflict, (await test.Admin.PutAsJsonAsync($"/api/auctions/{test.AuctionId}", current with { ReservePrice = 160m })).StatusCode);
        test.Clock.Set(test.Ends);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Bid(buyer, 120m)).StatusCode);
    }

    [Theory]
    [InlineData(null, null, "NoBids")]
    [InlineData(150, 100, "ReserveNotMet")]
    [InlineData(150, 150, "Sold")]
    public async Task Closure_records_one_immutable_result_and_never_reopens(int? reserve, int? amount, string outcome)
    {
        await using var test = await AuctionCase.CreateAsync(reserve);
        await test.Publish(); test.Clock.Set(test.Starts);
        using var buyer = await test.BuyerAsync();
        if (amount.HasValue) Assert.True((await test.Bid(buyer, amount.Value)).IsSuccessStatusCode);
        test.Clock.Set(test.Ends);
        var before = await test.Snapshot();
        Assert.Equal(HttpStatusCode.NoContent, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { before.Revision })).StatusCode);
        var result = await test.Snapshot();
        Assert.Equal("Closed", result.State); Assert.Equal(outcome, result.Outcome);
        Assert.Equal(outcome == "Sold", result.WinnerId.HasValue);
        test.Clock.Set(test.Ends.AddHours(2));
        Assert.Equal(HttpStatusCode.NoContent, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { before.Revision })).StatusCode);
        Assert.Equal(result.ClosedAt, (await test.Snapshot()).ClosedAt);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/open", new { result.Revision })).StatusCode);
        using var scope = test.Files.App.Server.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuctionSettlement>().SettleAsync(test.AuctionId, CancellationToken.None);
        Assert.Equal(result, await test.Snapshot());
    }

    [Theory]
    [InlineData("type")][InlineData("sealed")][InlineData("dates")][InlineData("increment")][InlineData("reserve")][InlineData("start-price")]
    public async Task Invalid_or_unsupported_configuration_is_rejected(string change)
    {
        await using var files = await FileCase.CreateAsync();
        await files.SetState(ListingComplianceStatus.Approved, true);
        var clock = files.App.Server.Services.GetRequiredService<TestAuctionClock>();
        var dto = new AuctionDto(Guid.Empty, files.Id, AuctionType.Timed, clock.GetUtcNow().AddMinutes(5), clock.GetUtcNow().AddHours(1), 100m, null, null, false, true, ListingRevision: (await files.Snapshot()).Revision);
        dto = change switch { "type" => dto with { Type = AuctionType.BuyNow }, "sealed" => dto with { IsSealedBid = true }, "dates" => dto with { EndsAt = dto.StartsAt },
            "increment" => dto with { MinimumIncrement = .001m }, "reserve" => dto with { ReservePrice = 99m }, _ => dto with { StartingPrice = 101m } };
        Assert.Equal(HttpStatusCode.BadRequest, (await files.Client.PostAsJsonAsync("/api/auctions", dto)).StatusCode);
    }

    [Fact]
    public async Task Ownership_admin_publication_self_bid_and_cancellation_guards_are_enforced()
    {
        await using var test = await AuctionCase.CreateAsync();
        var current = await test.Snapshot();
        Assert.Equal(HttpStatusCode.Forbidden, (await test.Files.Client.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/open", new { current.Revision })).StatusCode);
        using var otherSeller = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(await test.Files.App.AddUserAsync(test.Files.Tenant, FaunexRoles.Seller)));
        Assert.Equal(HttpStatusCode.Forbidden, (await otherSeller.GetAsync($"/api/auctions/{test.AuctionId}")).StatusCode);
        await test.Publish();
        var seller = (await test.Files.Client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{test.Id}"))!.SellerId;
        await test.Files.App.ChangeUserAsync(seller, async (users, user) => { Assert.True((await users.AddToRoleAsync(user, FaunexRoles.Buyer)).Succeeded); });
        using var scope = test.Files.App.Server.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Faunex.Api.Auth.ApplicationUser>>();
        using var self = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync((await identity.FindByIdAsync(seller.ToString()))!));
        test.Clock.Set(test.Starts);
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Bid(self, 100m)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/cancel", new { (await test.Snapshot()).Revision, Reason = "Too late" })).StatusCode);
    }

    [PostgresFact]
    public async Task Equal_concurrent_bids_commit_only_one_leader_and_one_bid()
    {
        await using var test = await AuctionCase.CreateAsync(); await test.Publish(); test.Clock.Set(test.Starts);
        using var first = await test.BuyerAsync(); using var second = await test.BuyerAsync();
        var responses = await Task.WhenAll(test.Bid(first, 100m), test.Bid(second, 100m));
        Assert.Single(responses, x => x.IsSuccessStatusCode);
        Assert.Single(responses, x => x.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest);
        using var scope = test.Files.App.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bids = await db.Bids.IgnoreQueryFilters().Where(x => x.AuctionId == test.AuctionId).ToListAsync();
        Assert.Equal(1, Assert.Single(bids).BidNumber); Assert.Equal(100m, (await test.Snapshot()).CurrentPrice);
        foreach (var response in responses) response.Dispose();
    }

    [PostgresFact]
    public async Task Duplicate_setup_cannot_create_duplicate_auctions()
    {
        await using var files = await FileCase.CreateAsync(); await files.SetState(ListingComplianceStatus.Approved, true);
        var clock = files.App.Server.Services.GetRequiredService<TestAuctionClock>();
        var input = new AuctionDto(Guid.Empty, files.Id, AuctionType.Timed, clock.GetUtcNow().AddMinutes(5), clock.GetUtcNow().AddHours(1), 100m, null, null, false, true, ListingRevision: (await files.Snapshot()).Revision);
        var responses = await Task.WhenAll(files.Client.PostAsJsonAsync("/api/auctions", input), files.Client.PostAsJsonAsync("/api/auctions", input));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        using var scope = files.App.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await db.Auctions.IgnoreQueryFilters().Where(x => x.ListingId == files.Id).ToListAsync());
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Draft_setup_allows_correction_and_reapproval_but_publishing_uses_current_approved_price()
    {
        await using var test = await AuctionCase.CreateAsync();
        var edit = (await test.Files.Client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{test.Id}"))!;
        Assert.False(edit.HasAuctionActivity);
        Assert.Equal(HttpStatusCode.NoContent, (await test.Files.Client.PutAsJsonAsync($"/api/birdlistings/{test.Id}", edit with { StartingPrice = 120m })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await test.Files.Client.PostAsJsonAsync($"/api/seller/listings/{test.Id}/submit", new { })).StatusCode);
        using var reviewer = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(await test.Files.App.AddUserAsync(null, FaunexRoles.PlatformComplianceAdmin)));
        var review = (await reviewer.GetFromJsonAsync<ListingReviewDto>($"/api/platform/compliance/listings/{test.Id}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await reviewer.PostAsJsonAsync($"/api/platform/compliance/listings/{test.Id}/approve", new { review.Revision, Notes = "Corrected price" })).StatusCode);
        var terms = await test.Snapshot();
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/open", new { terms.Revision })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await test.Admin.PutAsJsonAsync($"/api/auctions/{test.AuctionId}", terms with { StartingPrice = 120m })).StatusCode);
        await test.Publish();
    }

    [Fact]
    public async Task Publication_during_file_scan_blocks_the_file_change_without_orphan_bytes()
    {
        await using var test = await AuctionCase.CreateAsync();
        var revision = (await test.Files.Snapshot()).Revision;
        test.Files.App.Server.Services.GetRequiredService<TestUploadScanner>().Handler = test.Publish;
        Assert.Equal(HttpStatusCode.Conflict, (await ListingFileTests.UploadAsync(test.Files.Client, test.Id, revision, ListingFileTests.Png(), "bird.png", "image/png")).StatusCode);
        Assert.Empty(test.Files.Store.Files);
        Assert.Equal("Scheduled", (await test.Snapshot()).State);
    }

    [Fact]
    public async Task Cancellation_needs_reason_and_legacy_or_sealed_records_are_not_public_or_biddable()
    {
        await using var test = await AuctionCase.CreateAsync(); await test.Publish();
        var dto = await test.Snapshot();
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/cancel", new { dto.Revision, Reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/cancel", new { dto.Revision, Reason = "Withdrawn before bidding" })).StatusCode);
        Assert.Equal("Cancelled", (await test.Snapshot()).Outcome);
        using (var scope = test.Files.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entity = await db.Auctions.IgnoreQueryFilters().SingleAsync(x => x.Id == test.AuctionId);
            entity.RulesVersion = 0; entity.IsSealedBid = true; await db.SaveChangesAsync();
        }
        Assert.Empty((await test.Files.App.Client.GetFromJsonAsync<List<PublicAuctionDto>>($"/api/marketplace/listings/{test.Id}/auctions{test.HostQuery}"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await test.Files.App.Client.GetAsync($"/api/marketplace/auctions/{test.AuctionId}/bids{test.HostQuery}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await test.Files.App.Client.GetAsync($"/api/marketplace/auctions/{test.AuctionId}/price{test.HostQuery}")).StatusCode);
        using var buyer = await test.BuyerAsync(); test.Clock.Set(test.Starts);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Bid(buyer, 100m)).StatusCode);
    }

    [Fact]
    public async Task Foreign_tenants_cannot_read_config_bid_manage_or_read_public_auction_data()
    {
        await using var test = await AuctionCase.CreateAsync(); await test.Publish(); test.Clock.Set(test.Starts);
        var terms = await test.Snapshot();
        var foreign = await test.Files.App.AddTenantAsync();
        using var admin = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(await test.Files.App.AddUserAsync(foreign, FaunexRoles.TenantAdmin)));
        using var buyer = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(await test.Files.App.AddUserAsync(foreign, FaunexRoles.Buyer)));
        Assert.Equal(HttpStatusCode.NotFound, (await test.Bid(buyer, 100m)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/auctions/{test.AuctionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/auctions/{test.AuctionId}", terms)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/open", new { terms.Revision })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { terms.Revision })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/cancel", new { terms.Revision, Reason = "Foreign cancellation" })).StatusCode);
        var host = AccountApiFixture.TenantHost(foreign);
        Assert.Equal(HttpStatusCode.NotFound, (await test.Files.App.Client.GetAsync($"/api/marketplace/listings/{test.Id}/auctions?host={host}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await test.Files.App.Client.GetAsync($"/api/marketplace/auctions/{test.AuctionId}/bids?host={host}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await test.Files.App.Client.GetAsync($"/api/marketplace/auctions/{test.AuctionId}/price?host={host}")).StatusCode);
    }

    [Fact]
    public async Task Changed_listing_currency_requires_explicit_term_resave_without_conversion()
    {
        await using var test = await AuctionCase.CreateAsync(150m);
        using (var scope = test.Files.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Listings.IgnoreQueryFilters().SingleAsync(x => x.Id == test.Id)).CurrencyCode = "USD";
            await db.SaveChangesAsync();
        }
        var terms = await test.Snapshot(); Assert.Equal("ZAR", terms.CurrencyCode);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/open", new { terms.Revision })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await test.Admin.PutAsJsonAsync($"/api/auctions/{test.AuctionId}", terms)).StatusCode);
        var updated = await test.Snapshot(); Assert.Equal("USD", updated.CurrencyCode); Assert.Equal(150m, updated.ReservePrice);
        await test.Publish(); test.Clock.Set(test.Starts);
        using var buyer = await test.BuyerAsync();
        var response = await buyer.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/bids", new { Amount = 100m });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("USD", (await response.Content.ReadFromJsonAsync<BidDto>())!.CurrencyCode);
    }

    [Fact]
    public async Task Compatibility_bid_response_is_the_callers_exact_bid_even_when_timestamps_tie()
    {
        await using var test = await AuctionCase.CreateAsync(); await test.Publish(); test.Clock.Set(test.Starts);
        Guid? previous = null;
        foreach (var amount in new[] { 100m, 110m })
        {
            var user = await test.Files.App.AddUserAsync(test.Files.Tenant, FaunexRoles.Buyer);
            using var buyer = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(user));
            var response = await buyer.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/bids", new { Amount = amount });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var bid = (await response.Content.ReadFromJsonAsync<BidDto>())!;
            Assert.Equal(user.Id, bid.BidderId); Assert.Equal(amount, bid.Amount); Assert.NotEqual(previous, bid.Id); previous = bid.Id;
        }
    }

    [PostgresFact]
    public async Task Two_settlement_workers_record_one_result_even_if_tenant_is_inactive()
    {
        await using var test = await AuctionCase.CreateAsync(); await test.Publish(); test.Clock.Set(test.Starts);
        using var buyer = await test.BuyerAsync(); Assert.True((await test.Bid(buyer, 100m)).IsSuccessStatusCode); test.Clock.Set(test.Ends);
        using (var scope = test.Files.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Tenants.SingleAsync(x => x.Id == test.Files.Tenant)).IsActive = false; await db.SaveChangesAsync();
            Assert.Contains(test.AuctionId, await scope.ServiceProvider.GetRequiredService<AuctionSettlement>().DueAsync(CancellationToken.None));
        }
        async Task<Exception?> Close()
        {
            using var scope = test.Files.App.Server.Services.CreateScope();
            return await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<AuctionSettlement>().SettleAsync(test.AuctionId, CancellationToken.None));
        }
        var errors = await Task.WhenAll(Close(), Close());
        Assert.All(errors, x => Assert.True(x is null or DbUpdateConcurrencyException));
        using var finalScope = test.Files.App.Server.Services.CreateScope();
        var result = await finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Auctions.IgnoreQueryFilters().SingleAsync(x => x.Id == test.AuctionId);
        Assert.Equal("Sold", result.Outcome); Assert.NotNull(result.WinningBidId); Assert.NotNull(result.WinnerId);
    }
}

internal sealed class TestAuctionClock : TimeProvider
{
    private long _ticks = DateTimeOffset.UtcNow.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public void Set(DateTimeOffset value) => Interlocked.Exchange(ref _ticks, value.UtcTicks);
}

internal sealed class AuctionCase : IAsyncDisposable
{
    public FileCase Files { get; private init; } = null!;
    public HttpClient Admin { get; private init; } = null!;
    public Guid Id => Files.Id;
    public string HostQuery => "?host=" + AccountApiFixture.TenantHost(Files.Tenant);
    public Guid AuctionId { get; private init; }
    public TestAuctionClock Clock => Files.App.Server.Services.GetRequiredService<TestAuctionClock>();
    public DateTimeOffset Starts { get; private init; }
    public DateTimeOffset Ends { get; private init; }
    public async Task<AuctionDto> Snapshot() => (await Admin.GetFromJsonAsync<AuctionDto>($"/api/auctions/{AuctionId}"))!;
    public async Task<HttpClient> BuyerAsync() => Files.App.AuthenticatedClient(await Files.App.TokenAsync(await Files.App.AddUserAsync(Files.Tenant, FaunexRoles.Buyer)));
    public Task<HttpResponseMessage> Bid(HttpClient client, decimal amount) => client.PostAsJsonAsync("/api/buyer/bids", new CreateBidRequest(Id, amount));
    public async Task Publish() { var dto = await Snapshot(); Assert.Equal(HttpStatusCode.NoContent, (await Admin.PostAsJsonAsync($"/api/auctions/{AuctionId}/open", new { dto.Revision })).StatusCode); }
    public static async Task<AuctionCase> CreateAsync(decimal? reserve = null)
    {
        var files = await FileCase.CreateAsync(); await files.SetState(ListingComplianceStatus.Approved, true);
        var admin = files.App.AuthenticatedClient(await files.App.TokenAsync(await files.App.AddUserAsync(files.Tenant, FaunexRoles.TenantAdmin)));
        var clock = files.App.Server.Services.GetRequiredService<TestAuctionClock>();
        var start = clock.GetUtcNow().AddMinutes(5); var end = start.AddHours(1);
        var dto = new AuctionDto(Guid.Empty, files.Id, AuctionType.Timed, start, end, 100m, reserve, null, false, false, ListingRevision: (await files.Snapshot()).Revision);
        var response = await admin.PostAsJsonAsync("/api/auctions", dto); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return new() { Files = files, Admin = admin, AuctionId = await response.Content.ReadFromJsonAsync<Guid>(), Starts = start, Ends = end };
    }
    public async ValueTask DisposeAsync() { Admin.Dispose(); await Files.DisposeAsync(); }
}
