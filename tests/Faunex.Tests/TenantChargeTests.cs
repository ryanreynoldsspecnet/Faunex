using System.Net;
using System.Net.Http.Json;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class TenantChargeTests
{
    private const string CatalogPath = "/api/tenant/charges";
    private static ChargeCatalogDto Catalog() => new(Guid.Empty, true, "ZAR", "Test tenant: all listed amounts are fixed per lot.",
        [new(Guid.Empty, "Collection", "Delivery", 0), new(Guid.Empty, "Small cage delivery", "Delivery", 100, "Test delivery conditions"),
         new(Guid.Empty, "Handling", "RequiredExtra", 10), new(Guid.Empty, "Travel crate", "OptionalExtra", 25)]);
    private static async Task<ChargeCatalogDto> Save(HttpClient admin, ChargeCatalogDto input)
    {
        var response = await admin.PutAsJsonAsync(CatalogPath, input); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChargeCatalogDto>())!;
    }
    private static PriceSelectionRequest Select(ChargeCatalogDto catalog, bool collection = false, bool optional = true) => new(catalog.Revision,
        catalog.Options.Single(x => x.Name == (collection ? "Collection" : "Small cage delivery")).Id,
        optional ? [catalog.Options.Single(x => x.Kind == "OptionalExtra").Id] : []);
    private static async Task<AuctionOrderDto> Close(AuctionCase test, HttpClient winner)
    {
        test.Clock.Set(test.Starts); Assert.True((await test.Bid(winner, 100m)).IsSuccessStatusCode); test.Clock.Set(test.Ends);
        var response = await test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/close", new { (await test.Snapshot()).Revision });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return Assert.Single((await winner.GetFromJsonAsync<PagedResult<AuctionOrderDto>>("/api/orders"))!.Items);
    }

    [Fact]
    public async Task Catalogue_revision_validation_removal_and_tenant_boundaries_are_enforced()
    {
        await using var test = await AuctionCase.CreateAsync();
        Assert.Equal(Guid.Empty, (await test.Admin.GetFromJsonAsync<ChargeCatalogDto>(CatalogPath))!.Revision);
        var saved = await Save(test.Admin, Catalog()); Assert.All(saved.Options, x => Assert.NotEqual(Guid.Empty, x.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await test.Admin.PutAsJsonAsync(CatalogPath, Catalog())).StatusCode);
        var invalid = saved with { Options = saved.Options.Select(x => x with { Id = Guid.NewGuid() }).ToList() };
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Admin.PutAsJsonAsync(CatalogPath, invalid)).StatusCode);
        using var buyer = await test.BuyerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.GetAsync(CatalogPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.PutAsJsonAsync(CatalogPath, Catalog())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await test.Files.Client.GetAsync(CatalogPath)).StatusCode);
        using var platform = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(await test.Files.App.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin)));
        Assert.Equal(HttpStatusCode.Forbidden, (await platform.GetAsync(CatalogPath)).StatusCode);
        var foreign = await test.Files.App.AddTenantAsync();
        using var admin = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(await test.Files.App.AddUserAsync(foreign, FaunexRoles.TenantAdmin)));
        Assert.Empty((await admin.GetFromJsonAsync<ChargeCatalogDto>(CatalogPath))!.Options);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(CatalogPath, saved with { Revision = Guid.Empty })).StatusCode);
        saved = await Save(test.Admin, saved with { Options = saved.Options.Where(x => x.Kind != "OptionalExtra").ToList() });
        Assert.Equal(3, saved.Options.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Admin.PutAsJsonAsync(CatalogPath, saved with { Options = [] })).StatusCode);
        Assert.Empty((await Save(test.Admin, saved with { IsPublished = false, Options = [] })).Options);
    }

    [Theory]
    [InlineData("negative")][InlineData("precision")][InlineData("kind")][InlineData("name")][InlineData("terms")]
    [InlineData("currency")][InlineData("delivery")][InlineData("limit")][InlineData("duplicate")]
    public async Task Invalid_or_incomplete_catalogues_are_rejected(string change)
    {
        await using var test = await AuctionCase.CreateAsync(); var dto = Catalog();
        dto = change switch
        {
            "negative" => dto with { Options = [dto.Options[0] with { Amount = -1 }] },
            "precision" => dto with { Options = [dto.Options[0] with { Amount = 0.001m }] },
            "kind" => dto with { Options = [dto.Options[0] with { Kind = "Tax" }] },
            "name" => dto with { Options = [dto.Options[0] with { Name = "" }] },
            "terms" => dto with { Terms = "" }, "currency" => dto with { CurrencyCode = "EUR" },
            "delivery" => dto with { Options = dto.Options.Where(x => x.Kind != "Delivery").ToList() },
            "limit" => dto with { Options = Enumerable.Repeat(dto.Options[0], 41).ToList() },
            _ => dto with { Options = [dto.Options[0] with { Id = Guid.NewGuid() }] }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await test.Admin.PutAsJsonAsync(CatalogPath, dto)).StatusCode);
    }

    [Fact]
    public async Task Published_terms_and_accepted_itemised_totals_are_immutable_and_do_not_multiply_by_quantity()
    {
        await using var test = await AuctionCase.CreateAsync(); var catalog = await Save(test.Admin, Catalog());
        using (var scope = test.Files.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var listing = await db.Listings.IgnoreQueryFilters().SingleAsync();
            listing.Quantity = 4; await db.SaveChangesAsync();
        }
        await test.Publish();
        var publicAuction = Assert.Single((await test.Files.App.Client.GetFromJsonAsync<List<PublicAuctionDto>>($"/api/marketplace/listings/{test.Id}/auctions{test.HostQuery}"))!);
        Assert.Equal(catalog.Revision, publicAuction.Charges!.Revision); Assert.Equal(100m, publicAuction.Charges.Options.Single(x => x.Name == "Small cage delivery").Amount);
        var changed = await Save(test.Admin, catalog with { Terms = "Changed later", Options = catalog.Options.Select(x => x with { Amount = x.Amount + 1000 }).ToList() });
        Assert.NotEqual(catalog.Revision, changed.Revision);
        using var winner = await test.BuyerAsync(); var order = await Close(test, winner);
        Assert.Equal(4, order.Quantity);
        var path = $"/api/orders/{order.Id}/pricing"; var selection = Select(catalog);
        var options = (await winner.GetFromJsonAsync<ChargeTermsDto>(path + "/options"))!;
        Assert.Equal(catalog.Revision, options.Revision);
        var preview = (await (await winner.PostAsJsonAsync(path + "/preview", selection)).Content.ReadFromJsonAsync<PriceBreakdownDto>())!;
        Assert.Equal(100m, preview.Subtotal); Assert.Equal(135m, preview.AdditionalCharges); Assert.Equal(235m, preview.Total); Assert.Equal(3, preview.Lines.Count);
        Assert.Equal(HttpStatusCode.Conflict, (await winner.PostAsJsonAsync(path + "/finalise", new AcceptPriceRequest(selection, 1m))).StatusCode);
        var response = await winner.PostAsJsonAsync(path + "/finalise", new AcceptPriceRequest(selection, 235m)); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = (await response.Content.ReadFromJsonAsync<OrderQuoteDto>())!;
        var repeat = (await (await winner.PostAsJsonAsync(path + "/finalise", new AcceptPriceRequest(selection, 235m))).Content.ReadFromJsonAsync<OrderQuoteDto>())!;
        Assert.Equal(quote.Id, repeat.Id); Assert.Equal(quote.AcceptedAt, repeat.AcceptedAt);
        Assert.Equal(HttpStatusCode.Conflict, (await winner.PostAsJsonAsync(path + "/finalise", new AcceptPriceRequest(Select(catalog, true, false), 110m))).StatusCode);
        var read = (await winner.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{order.Id}"))!;
        Assert.Equal("Finalised", read.PricingStatus); Assert.Equal(235m, read.Quote!.Pricing.Total); Assert.Equal(100m, read.Amount); Assert.False(read.CheckoutAvailable);
        Assert.Equal(quote.Id, (await test.Admin.GetFromJsonAsync<AuctionOrderDto>($"/api/orders/{order.Id}"))!.Quote!.Id);
        using var finalScope = test.Files.App.Server.Services.CreateScope(); var final = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await final.OrderPriceQuotes.IgnoreQueryFilters().SingleAsync(); stored.PricingJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => final.SaveChangesAsync());
    }

    [Fact]
    public async Task Free_collection_is_explicit_required_extras_apply_and_foreign_or_duplicate_choices_fail()
    {
        await using var test = await AuctionCase.CreateAsync(); var catalog = await Save(test.Admin, Catalog()); await test.Publish();
        using var winner = await test.BuyerAsync(); var order = await Close(test, winner); var path = $"/api/orders/{order.Id}/pricing";
        var selection = Select(catalog, true, false);
        var preview = (await (await winner.PostAsJsonAsync(path + "/preview", selection)).Content.ReadFromJsonAsync<PriceBreakdownDto>())!;
        Assert.Equal(110m, preview.Total); Assert.Contains(preview.Lines, x => x.Name == "Handling"); Assert.Contains(preview.Lines, x => x.Name == "Collection" && x.Amount == 0);
        foreach (var invalid in new[] { selection with { DeliveryOptionId = Guid.Empty }, selection with { OptionalChargeIds = [Guid.NewGuid()] },
            selection with { OptionalChargeIds = [catalog.Options[3].Id, catalog.Options[3].Id] }, selection with { OptionalChargeIds = [catalog.Options[2].Id] } })
            Assert.Equal(HttpStatusCode.BadRequest, (await winner.PostAsJsonAsync(path + "/preview", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await winner.PostAsJsonAsync(path + "/preview", selection with { CatalogRevision = Guid.NewGuid() })).StatusCode);
        using var loser = await test.BuyerAsync(); Assert.Equal(HttpStatusCode.NotFound, (await loser.GetAsync(path + "/options")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await loser.PostAsJsonAsync(path + "/finalise", new AcceptPriceRequest(selection, 110m))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await test.Admin.PostAsJsonAsync(path + "/preview", selection)).StatusCode);
        var foreign = await test.Files.App.AddTenantAsync(); using var buyer = test.Files.App.AuthenticatedClient(await test.Files.App.TokenAsync(await test.Files.App.AddUserAsync(foreign, FaunexRoles.Buyer)));
        Assert.Equal(HttpStatusCode.NotFound, (await buyer.GetAsync(path + "/options")).StatusCode);
    }

    [Fact]
    public async Task Old_auctions_cannot_receive_retroactive_fees_and_draft_or_wrong_currency_policies_block_new_publication()
    {
        await using var test = await AuctionCase.CreateAsync(); await test.Publish(); using var buyer = await test.BuyerAsync(); var order = await Close(test, buyer);
        var catalog = await Save(test.Admin, Catalog());
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.GetAsync($"/api/orders/{order.Id}/pricing/options")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.PostAsJsonAsync($"/api/orders/{order.Id}/pricing/finalise", new AcceptPriceRequest(Select(catalog), 235m))).StatusCode);
        await using var fresh = await AuctionCase.CreateAsync(); var draft = await Save(fresh.Admin, Catalog() with { IsPublished = false });
        Assert.Equal(HttpStatusCode.Conflict, (await fresh.Admin.PostAsJsonAsync($"/api/auctions/{fresh.AuctionId}/open", new { (await fresh.Snapshot()).Revision })).StatusCode);
        await Save(fresh.Admin, draft with { IsPublished = true, CurrencyCode = "USD" });
        Assert.Equal(HttpStatusCode.Conflict, (await fresh.Admin.PostAsJsonAsync($"/api/auctions/{fresh.AuctionId}/open", new { (await fresh.Snapshot()).Revision })).StatusCode);
    }

    [PostgresFact]
    public async Task Competing_confirmations_record_one_quote_and_retry_returns_it()
    {
        await using var test = await AuctionCase.CreateAsync(); var catalog = await Save(test.Admin, Catalog()); await test.Publish();
        using var winner = await test.BuyerAsync(); var order = await Close(test, winner); var path = $"/api/orders/{order.Id}/pricing/finalise";
        var request = new AcceptPriceRequest(Select(catalog), 235m);
        var results = await Task.WhenAll(winner.PostAsJsonAsync(path, request), winner.PostAsJsonAsync(path, request));
        Assert.Contains(results, x => x.StatusCode == HttpStatusCode.OK); Assert.All(results, x => Assert.True(x.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
        using var scope = test.Files.App.Server.Services.CreateScope(); Assert.Single(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OrderPriceQuotes.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(HttpStatusCode.OK, (await winner.PostAsJsonAsync(path, request)).StatusCode);
        foreach (var result in results) result.Dispose();
    }

    [PostgresFact]
    public async Task Concurrent_catalogue_saves_and_publication_preserve_complete_recorded_terms()
    {
        await using var test = await AuctionCase.CreateAsync(); var catalog = await Save(test.Admin, Catalog());
        var a = test.Admin.PutAsJsonAsync(CatalogPath, catalog with { Terms = "New complete terms", Options = catalog.Options.Select(x => x with { Amount = 222m }).ToList() });
        var b = test.Admin.PostAsJsonAsync($"/api/auctions/{test.AuctionId}/open", new { (await test.Snapshot()).Revision });
        var results = await Task.WhenAll(a, b);
        Assert.True(results[0].StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict);
        Assert.True(results[1].StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict);
        if (results[1].StatusCode == HttpStatusCode.NoContent)
        {
            var snapshot = Assert.Single((await test.Files.App.Client.GetFromJsonAsync<List<PublicAuctionDto>>($"/api/marketplace/listings/{test.Id}/auctions{test.HostQuery}"))!).Charges!;
            Assert.Equal(snapshot.Terms == "New complete terms" ? 222m : 100m, snapshot.Options.Single(x => x.Name == "Small cage delivery").Amount);
        }
        foreach (var response in results) response.Dispose();
    }
}
