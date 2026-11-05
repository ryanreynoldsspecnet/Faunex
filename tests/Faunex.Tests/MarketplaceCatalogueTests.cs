using System.Net;
using System.Net.Http.Json;
using Faunex.Application.DTOs;
using Faunex.Application.Tenancy;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class MarketplaceCatalogueTests
{
    private static string Path(Guid tenant, string options = "") => $"/api/marketplace/catalogue?host={AccountApiFixture.TenantHost(tenant)}&sort=name{options}";

    [Fact]
    public async Task Catalogue_only_exposes_approved_active_tenant_lots_and_public_photo_ids()
    {
        await using var test = await FileCase.CreateAsync();
        var uploaded = await ListingFileTests.UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision,
            ListingFileTests.Png(), "bird.png", "image/png");
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var photo = Assert.Single((await uploaded.Content.ReadFromJsonAsync<ListingFilesDto>())!.Files);
        var documentUpload = await ListingFileTests.UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision,
            ListingFileTests.Pdf(), "permit.pdf", "application/pdf", photo: false);
        Assert.Equal(HttpStatusCode.OK, documentUpload.StatusCode);
        var document = (await documentUpload.Content.ReadFromJsonAsync<ListingFilesDto>())!.Files.Single(x => !x.IsPhoto);
        var before = (await test.App.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(Path(test.Tenant)))!;
        Assert.Empty(before.Items); Assert.Equal(0, before.Total);
        await test.SetState(ListingComplianceStatus.Approved, true);
        var response = await test.App.Client.GetAsync(Path(test.Tenant));
        Assert.True(response.Headers.CacheControl!.NoStore);
        var page = (await response.Content.ReadFromJsonAsync<MarketplaceCatalogueDto>())!;
        var lot = Assert.Single(page.Items);
        Assert.Equal(1, page.Total); Assert.Equal(test.Id, lot.Id); Assert.Equal(photo.Id, lot.PhotoId); Assert.Equal("bird", lot.Category); Assert.Null(lot.Auction);
        var json = await response.Content.ReadAsStringAsync();
        foreach (var hidden in new[] { "storageKey", "sha256", "sellerId", "uploadedByUserId" }) Assert.DoesNotContain(hidden, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(document.Id.ToString(), json);
        Assert.Equal(HttpStatusCode.OK, (await test.App.Client.GetAsync($"/api/marketplace/photos/{lot.PhotoId}")).StatusCode);
        var foreign = await test.App.AddTenantAsync();
        using var other = test.App.Server.CreateClient(); other.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(foreign));
        Assert.Empty((await other.GetFromJsonAsync<MarketplaceCatalogueDto>(Path(foreign)))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/marketplace/photos/{photo.Id}")).StatusCode);
        await test.SetState(ListingComplianceStatus.Approved, false);
        test.App.Client.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader);
        test.App.Client.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(test.Tenant));
        Assert.Empty((await test.App.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(Path(test.Tenant)))!.Items);
    }

    [Fact]
    public async Task Search_category_and_pagination_are_applied_before_counts_and_page_enrichment()
    {
        await using var test = await FileCase.CreateAsync(); await test.SetState(ListingComplianceStatus.Approved, true);
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var original = await db.Listings.IgnoreQueryFilters().SingleAsync();
            for (var i = 0; i < 60; i++)
            {
                var row = new Listing { TenantId = test.Tenant, SellerId = original.SellerId, Title = $"Catalogue lot {i:00}", StartingPrice = i + 1, CurrencyCode = "ZAR", Location = "Pretoria" };
                row.Compliance = new ListingCompliance { TenantId = test.Tenant, ListingId = row.Id, Status = ListingComplianceStatus.Approved };
                db.Add(row);
            }
            await db.SaveChangesAsync();
        }
        var page = (await test.App.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(Path(test.Tenant, "&skip=48&take=100")))!;
        Assert.Equal(61, page.Total); Assert.Equal(13, page.Items.Count);
        var capped = (await test.App.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(Path(test.Tenant, "&take=100")))!;
        Assert.Equal(48, capped.Items.Count); Assert.Equal(61, capped.Total);
        var birds = (await test.App.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(Path(test.Tenant, "&category=bird&search=FILE")))!;
        Assert.Equal(test.Id, Assert.Single(birds.Items).Id); Assert.Equal(1, birds.Total);
        var location = (await test.App.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(Path(test.Tenant, "&category=other&search=pReToRiA&take=1")))!;
        Assert.Equal(60, location.Total); Assert.Single(location.Items);
        Assert.Empty((await test.App.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(Path(test.Tenant, "&search=missing-lot")))!.Items);
    }

    [Theory]
    [InlineData("&category=invalid")][InlineData("&status=invalid")][InlineData("&sort=invalid")][InlineData("&search=")]
    public async Task Invalid_filters_and_oversized_search_are_rejected(string option)
    {
        await using var test = await FileCase.CreateAsync();
        if (option == "&search=") option += new string('x', 201);
        // Explicit invalid sort replaces the default instead of relying on duplicate query keys.
        var path = option.StartsWith("&sort", StringComparison.Ordinal) ? $"/api/marketplace/catalogue?host={AccountApiFixture.TenantHost(test.Tenant)}{option}" : Path(test.Tenant, option);
        Assert.Equal(HttpStatusCode.BadRequest, (await test.App.Client.GetAsync(path)).StatusCode);
    }

    [PostgresFact]
    public async Task Auction_filters_and_all_sorts_use_published_authoritative_states_without_private_terms()
    {
        await using var test = await AuctionCase.CreateAsync();
        var path = $"/api/marketplace/catalogue?host={AccountApiFixture.TenantHost(test.Files.Tenant)}";
        async Task<MarketplaceCatalogueDto> Read(string options) => (await test.Files.App.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(path + options))!;
        Assert.Null(Assert.Single((await Read("&sort=name")).Items).Auction);
        Assert.Empty((await Read("&status=upcoming")).Items);
        await test.Publish();
        Assert.Equal("Scheduled", Assert.Single((await Read("&status=upcoming")).Items).Auction!.State);
        foreach (var sort in new[] { "recent", "name", "price", "ending" }) Assert.Single((await Read("&sort=" + sort)).Items);
        Assert.Empty((await Read("&status=open")).Items);
        test.Clock.Set(test.Starts);
        Assert.Equal("Live", Assert.Single((await Read("&status=open")).Items).Auction!.State);
        Assert.Empty((await Read("&status=upcoming")).Items);
        using var buyer = await test.BuyerAsync(); Assert.True((await test.Bid(buyer, 100m)).IsSuccessStatusCode);
        Assert.Equal(100m, Assert.Single((await Read("&status=open")).Items).Auction!.CurrentPrice);
        test.Clock.Set(test.Ends);
        Assert.Equal("AwaitingResult", Assert.Single((await Read("&sort=name")).Items).Auction!.State);
        Assert.Empty((await Read("&status=open")).Items); Assert.Empty((await Read("&status=results")).Items);
        Assert.True((await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { (await test.Snapshot()).Revision })).IsSuccessStatusCode);
        Assert.Equal("Sold", Assert.Single((await Read("&status=results")).Items).Auction!.Outcome);
        var json = await test.Files.App.Client.GetStringAsync(path);
        Assert.DoesNotContain("reservePrice", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("bidderId", json, StringComparison.OrdinalIgnoreCase);
    }
}
