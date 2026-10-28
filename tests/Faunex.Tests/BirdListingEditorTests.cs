using System.Net;
using System.Net.Http.Json;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class BirdListingEditorTests
{
    [Theory]
    [InlineData(FaunexRoles.TenantAdmin)]
    [InlineData(FaunexRoles.Seller)]
    public async Task Create_and_edit_bird_draft_persists_fields_without_publishing(string role)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        var user = await app.AddUserAsync(tenant, role);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));
        var species = await SpeciesAsync(app);
        var created = await client.PostAsJsonAsync("/api/seller/listings/bird", new CreateBirdListingRequest(
            species, "African grey", "Healthy bird", 1500.50m, true, user.Id, Quantity: 2, Location: "Cape Town",
            RingNumber: "ZA-123", MicrochipNumber: "CHIP-123", Sex: "Female", HatchDate: new DateOnly(2024, 2, 1)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var listing = (await created.Content.ReadFromJsonAsync<ListingDto>())!;
        var original = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{listing.Id}"))!;
        Assert.Equal("ZAR", original.CurrencyCode);
        Assert.Equal(2, original.Quantity);
        Assert.Equal("Cape Town", original.Location);
        Assert.Equal("ZA-123", original.RingNumber);
        Assert.Equal("CHIP-123", original.MicrochipNumber);
        Assert.Equal("Female", original.Sex);
        Assert.Equal(new DateOnly(2024, 2, 1), original.HatchDate);
        Assert.Equal("Draft", original.ComplianceStatus);
        Assert.False(original.IsActive);
        Assert.NotNull(original.Revision);
        var update = original with { Title = "Updated African grey", StartingPrice = 2000m, BuyNowPrice = 2000m, Location = "Pretoria", Quantity = 3, IsActive = true };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/birdlistings/{listing.Id}", update)).StatusCode);
        var edited = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{listing.Id}"))!;
        Assert.Equal("Updated African grey", edited.Title);
        Assert.Equal("Pretoria", edited.Location);
        Assert.Equal(3, edited.Quantity);
        Assert.False(edited.IsActive);
        Assert.NotEqual(original.Revision, edited.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/birdlistings/{listing.Id}", update)).StatusCode);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("price")]
    [InlineData("precision")]
    [InlineData("quantity")]
    [InlineData("currency")]
    [InlineData("species")]
    [InlineData("hatch")]
    [InlineData("sex")]
    [InlineData("ring")]
    public async Task Validation_is_enforced_on_both_creation_routes_and_updates(string invalid)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        var seller = await app.AddUserAsync(tenant, FaunexRoles.Seller);
        using var client = app.AuthenticatedClient(await app.TokenAsync(seller));
        var id = await CreateAsync(app, client, seller.Id);
        var valid = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{id}"))!;
        var bad = invalid switch
        {
            "title" => valid with { Title = " " },
            "description" => valid with { Description = new string('x', 2001) },
            "price" => valid with { StartingPrice = -1 },
            "precision" => valid with { StartingPrice = 1.001m },
            "quantity" => valid with { Quantity = 0 },
            "currency" => valid with { CurrencyCode = "FAKE" },
            "species" => valid with { SpeciesId = Guid.NewGuid() },
            "hatch" => valid with { HatchDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) },
            "sex" => valid with { Sex = "invalid" },
            _ => valid with { RingNumber = new string('x', 81) }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/birdlistings", bad)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/birdlistings/{id}", bad)).StatusCode);
        var canonical = new CreateBirdListingRequest(bad.SpeciesId, bad.Title, bad.Description, bad.StartingPrice,
            true, seller.Id, bad.CurrencyCode, bad.Quantity, bad.Location, bad.RingNumber, bad.MicrochipNumber, bad.Sex, bad.HatchDate);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/seller/listings/bird", canonical)).StatusCode);
    }

    [Fact]
    public async Task Foreign_sellers_buyers_platform_and_other_tenants_cannot_mutate_or_read_private_draft()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        var seller = await app.AddUserAsync(tenant, FaunexRoles.Seller);
        using var owner = app.AuthenticatedClient(await app.TokenAsync(seller));
        var id = await CreateAsync(app, owner, seller.Id);
        var draft = (await owner.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{id}"))!;
        var otherSeller = await app.AddUserAsync(tenant, FaunexRoles.Seller);
        using var other = app.AuthenticatedClient(await app.TokenAsync(otherSeller));
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/birdlistings/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/api/birdlistings/{id}", draft)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsJsonAsync($"/api/birdlistings/{id}", draft with { SellerId = otherSeller.Id })).StatusCode);
        using var buyer = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(tenant, FaunexRoles.Buyer)));
        Assert.Equal(HttpStatusCode.NotFound, (await buyer.GetAsync($"/api/birdlistings/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await buyer.PutAsJsonAsync($"/api/birdlistings/{id}", draft)).StatusCode);
        using var platform = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin)));
        Assert.Equal(HttpStatusCode.Forbidden, (await platform.PutAsJsonAsync($"/api/birdlistings/{id}", draft)).StatusCode);
        using var foreign = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(await app.AddTenantAsync(), FaunexRoles.TenantAdmin)));
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/birdlistings/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.PutAsJsonAsync($"/api/birdlistings/{id}", draft)).StatusCode);
        using var admin = app.AuthenticatedClient(await app.TokenAsync(await app.AddUserAsync(tenant, FaunexRoles.TenantAdmin)));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/birdlistings/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/birdlistings/{id}", draft with { Title = "Admin corrected draft" })).StatusCode);
    }

    [Theory]
    [InlineData(ListingComplianceStatus.UnderReview)]
    [InlineData(ListingComplianceStatus.Suspended)]
    [InlineData(ListingComplianceStatus.Approved)]
    public async Task Review_states_are_enforced_and_editing_approved_listing_unpublishes_it(ListingComplianceStatus status)
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        var user = await app.AddUserAsync(tenant, FaunexRoles.TenantAdmin);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));
        var id = await CreateAsync(app, client, user.Id);
        using (var scope = app.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var listing = await db.Listings.IgnoreQueryFilters().Include(x => x.Compliance).SingleAsync(x => x.Id == id);
            listing.Compliance!.Status = status;
            listing.IsActive = status == ListingComplianceStatus.Approved;
            await db.SaveChangesAsync();
        }
        var dto = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{id}"))!;
        var response = await client.PutAsJsonAsync($"/api/birdlistings/{id}", dto with { Title = "Changed after review" });
        Assert.Equal(status == ListingComplianceStatus.Approved ? HttpStatusCode.NoContent : HttpStatusCode.Conflict, response.StatusCode);
        if (status == ListingComplianceStatus.Approved)
        {
            var updated = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{id}"))!;
            Assert.Equal("Draft", updated.ComplianceStatus);
            Assert.False(updated.IsActive);
            // PostgreSQL executes the public timestamp query; SQLite cannot aggregate DateTimeOffset.
            if (app.UsesPostgres)
                Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync($"/api/marketplace/listings/{id}?host={AccountApiFixture.TenantHost(tenant)}")).StatusCode);
        }
    }

    [Fact]
    public async Task Existing_auction_blocks_edit_and_old_USD_is_not_automatically_converted()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        var user = await app.AddUserAsync(tenant, FaunexRoles.Seller);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));
        var id = await CreateAsync(app, client, user.Id);
        var original = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{id}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/birdlistings/{id}", original with { CurrencyCode = "USD" })).StatusCode);
        using (var scope = app.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Auctions.Add(new Auction { ListingId = id, TenantId = tenant, StartingPrice = 100, IsClosed = true });
            await db.SaveChangesAsync();
        }
        var locked = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{id}"))!;
        Assert.Equal("USD", locked.CurrencyCode);
        Assert.True(locked.HasAuctionActivity);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/birdlistings/{id}", locked with { Title = "Cannot edit auction" })).StatusCode);
    }

    [Fact]
    public async Task Missing_bird_documents_stay_pending_and_can_be_resubmitted_without_bypassing_gate()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        using (var scope = app.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ListingDocumentRequirements.Add(new ListingDocumentRequirement
            {
                TenantId = tenant, AnimalClass = "bird", DocumentType = ListingDocumentType.VeterinaryCertificate
            });
            await db.SaveChangesAsync();
        }
        var seller = await app.AddUserAsync(tenant, FaunexRoles.Seller);
        using var client = app.AuthenticatedClient(await app.TokenAsync(seller));
        var id = await CreateAsync(app, client, seller.Id);
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/seller/listings/{id}/submit", new { })).StatusCode);
            var draft = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{id}"))!;
            Assert.Equal("PendingDocuments", draft.ComplianceStatus);
            Assert.False(draft.IsActive);
        }
    }

    [PostgresFact]
    public async Task Concurrent_saves_of_same_revision_cannot_both_overwrite_the_listing()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var user = await app.AddUserAsync(await app.AddTenantAsync(), FaunexRoles.TenantAdmin);
        using var client = app.AuthenticatedClient(await app.TokenAsync(user));
        var id = await CreateAsync(app, client, user.Id);
        var current = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{id}"))!;
        var responses = await Task.WhenAll(
            client.PutAsJsonAsync($"/api/birdlistings/{id}", current with { Title = "First edit" }),
            client.PutAsJsonAsync($"/api/birdlistings/{id}", current with { Title = "Second edit" }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Documents_are_optional_by_default_and_foreign_tenant_rules_do_not_apply()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var first = await app.AddTenantAsync();
        var second = await app.AddTenantAsync();
        using (var scope = app.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ListingDocumentRequirements.Add(new ListingDocumentRequirement
            {
                TenantId = second, AnimalClass = "bird", DocumentType = ListingDocumentType.CitesPermit
            });
            await db.SaveChangesAsync();
        }
        var seller = await app.AddUserAsync(first, FaunexRoles.Seller);
        using var client = app.AuthenticatedClient(await app.TokenAsync(seller));
        var id = await CreateAsync(app, client, seller.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/seller/listings/{id}/submit", new { })).StatusCode);
        var submitted = (await client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{id}"))!;
        Assert.Equal("UnderReview", submitted.ComplianceStatus);
        Assert.False(submitted.IsActive);
    }

    private static async Task<Guid> SpeciesAsync(AccountApiFixture app)
    {
        using var scope = app.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var species = new BirdSpecies { CommonName = "African grey", ScientificName = "Psittacus erithacus" };
        db.BirdSpeciesSet.Add(species);
        await db.SaveChangesAsync();
        return species.Id;
    }
    private static async Task<Guid> CreateAsync(AccountApiFixture app, HttpClient client, Guid user)
    {
        var response = await client.PostAsJsonAsync("/api/seller/listings/bird", new CreateBirdListingRequest(await SpeciesAsync(app), "Test bird draft", null, 100m, true, user));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ListingDto>())!.Id;
    }
}
