using System.Net;
using System.Net.Http.Json;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Application.Tenancy;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class AuctionCollectionTests
{
    private const string Path = "/api/tenant/collections";

    [Fact]
    public async Task Collections_group_public_lots_without_changing_live_auction_or_bid()
    {
        await using var test = await AuctionCase.CreateAsync();
        var app = test.Files.App;
        var admin = test.Admin;
        var friday = await Create(admin, "  Friday auction  ");
        Assert.Equal("Friday auction", friday.Name);
        using var duplicate = await admin.PostAsJsonAsync(Path, new CreateAuctionCollectionRequest("friday auction"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var tuesday = await Create(admin, "Tuesday special");

        await test.Publish();
        test.Clock.Set(test.Starts);
        using var buyer = await test.BuyerAsync();
        using var bid = await test.Bid(buyer, 100m);
        bid.EnsureSuccessStatusCode();
        var before = await test.Snapshot();
        var lotPath = $"{Path}/lots/{test.Id}";
        var assignment = (await admin.GetFromJsonAsync<LotCollectionDto>(lotPath))!;
        Assert.Null(assignment.CollectionId);
        assignment = await Assign(admin, lotPath, friday.Id, assignment.Revision);
        Assert.Equal(friday.Id, assignment.CollectionId);
        using var stale = await admin.PutAsJsonAsync(lotPath, new AssignLotCollectionRequest(tuesday.Id, Guid.Empty));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var catalogue = "/api/marketplace/catalogue" + test.HostQuery + "&sort=name";
        var selected = (await app.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(
            $"{catalogue}&collection={friday.Id}&category=bird&search=FILE&take=1"))!;
        Assert.Equal(1, selected.Total);
        Assert.Equal(test.Id, Assert.Single(selected.Items).Id);
        Assert.Equal(new AuctionCollectionOption(friday.Id, friday.Name), Assert.Single(selected.Collections));
        var paged = (await app.Client.GetFromJsonAsync<MarketplaceCatalogueDto>($"{catalogue}&collection={friday.Id}&skip=1"))!;
        Assert.Equal(1, paged.Total);
        Assert.Empty(paged.Items);

        var archived = await Update(admin, friday, "Friday catalogue", false);
        var hidden = (await app.Client.GetFromJsonAsync<MarketplaceCatalogueDto>($"{catalogue}&collection={friday.Id}"))!;
        Assert.Empty(hidden.Collections);
        Assert.Empty(hidden.Items);
        Assert.Equal(test.Id, Assert.Single((await app.Client.GetFromJsonAsync<MarketplaceCatalogueDto>(catalogue))!.Items).Id);
        Assert.Equal(friday.Id, (await admin.GetFromJsonAsync<LotCollectionDto>(lotPath))!.CollectionId);
        using var staleName = await admin.PutAsJsonAsync($"{Path}/{friday.Id}", new UpdateAuctionCollectionRequest("Old edit", true, friday.Revision));
        Assert.Equal(HttpStatusCode.Conflict, staleName.StatusCode);
        await Update(admin, archived, archived.Name, true);
        var restored = (await app.Client.GetFromJsonAsync<MarketplaceCatalogueDto>($"{catalogue}&collection={friday.Id}"))!;
        Assert.Equal("Friday catalogue", Assert.Single(restored.Collections).Name);
        Assert.Equal(test.Id, Assert.Single(restored.Items).Id);

        assignment = await Assign(admin, lotPath, tuesday.Id, assignment.Revision);
        Assert.Equal(tuesday.Id, assignment.CollectionId);
        await Update(admin, tuesday, tuesday.Name, false);
        assignment = await Assign(admin, lotPath, null, assignment.Revision);
        Assert.Null(assignment.CollectionId);
        using var inactive = await admin.PutAsJsonAsync(lotPath, new AssignLotCollectionRequest(tuesday.Id, assignment.Revision));
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        var after = await test.Snapshot();
        Assert.Equal(assignment.Revision, after.ListingRevision);
        Assert.Equal(before with { ListingRevision = after.ListingRevision }, after);
        using var scope = app.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recordedBid = await db.Bids.IgnoreQueryFilters().SingleAsync(x => x.AuctionId == test.AuctionId);
        Assert.Equal(100m, recordedBid.Amount);
    }

    [Fact]
    public async Task Only_own_tenant_admin_can_create_and_assign_collections_including_at_draft_creation()
    {
        await using var test = await FileCase.CreateAsync();
        var app = test.App;
        var actor = await app.AddUserAsync(test.Tenant, FaunexRoles.TenantAdmin);
        using var admin = app.AuthenticatedClient(await app.TokenAsync(actor));
        var own = await Create(admin, "Friday");
        var foreignId = await app.AddTenantAsync();
        using var other = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(foreignId, FaunexRoles.TenantAdmin)));
        var foreign = await Create(other, "Friday");
        Assert.Equal(own.Id, Assert.Single((await admin.GetFromJsonAsync<List<AuctionCollectionDto>>(Path))!).Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await test.Client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Path}/lots/{test.Id}")).StatusCode);
        using var foreignEdit = await admin.PutAsJsonAsync($"{Path}/{foreign.Id}", new UpdateAuctionCollectionRequest("Stolen", true, foreign.Revision));
        Assert.Equal(HttpStatusCode.NotFound, foreignEdit.StatusCode);
        var current = (await admin.GetFromJsonAsync<LotCollectionDto>($"{Path}/lots/{test.Id}"))!;
        using var crossAssignment = await admin.PutAsJsonAsync($"{Path}/lots/{test.Id}", new AssignLotCollectionRequest(foreign.Id, current.Revision));
        Assert.Equal(HttpStatusCode.BadRequest, crossAssignment.StatusCode);

        using var scope = app.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var species = await db.BirdSpeciesSet.Where(x => x.ScientificName == "Testus birdus").Select(x => x.Id).SingleAsync();
        var draft = new CreateBirdListingRequest(species, "Collection draft", null, 100m, true, actor.Id, CollectionId: foreign.Id);
        using var rejected = await admin.PostAsJsonAsync("/api/seller/listings/bird", draft);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(1, await db.Listings.IgnoreQueryFilters().CountAsync());
        using var accepted = await admin.PostAsJsonAsync("/api/seller/listings/bird", draft with { CollectionId = own.Id });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var id = (await accepted.Content.ReadFromJsonAsync<ListingDto>())!.Id;
        Assert.Equal(own.Id, (await admin.GetFromJsonAsync<LotCollectionDto>($"{Path}/lots/{id}"))!.CollectionId);

        app.Client.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader);
        app.Client.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(test.Tenant));
        var catalogue = $"/api/marketplace/catalogue?host={AccountApiFixture.TenantHost(test.Tenant)}&sort=name";
        var page = (await app.Client.GetFromJsonAsync<MarketplaceCatalogueDto>($"{catalogue}&collection={own.Id}"))!;
        Assert.Empty(page.Collections);
        Assert.Empty(page.Items);
        Assert.Empty((await app.Client.GetFromJsonAsync<MarketplaceCatalogueDto>($"{catalogue}&collection={foreign.Id}"))!.Items);
    }

    private static async Task<AuctionCollectionDto> Create(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(Path, new CreateAuctionCollectionRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuctionCollectionDto>())!;
    }

    private static async Task<AuctionCollectionDto> Update(HttpClient client, AuctionCollectionDto row, string name, bool active)
    {
        using var response = await client.PutAsJsonAsync($"{Path}/{row.Id}", new UpdateAuctionCollectionRequest(name, active, row.Revision));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuctionCollectionDto>())!;
    }

    private static async Task<LotCollectionDto> Assign(HttpClient client, string path, Guid? id, Guid revision)
    {
        using var response = await client.PutAsJsonAsync(path, new AssignLotCollectionRequest(id, revision));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LotCollectionDto>())!;
    }
}
