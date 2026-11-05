using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Application.Tenancy;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class BuyerWishlistTests
{
    [Fact]
    public async Task Multiple_lists_rename_membership_move_remove_and_delete_survive_fresh_clients()
    {
        await using var test = await FileCase.CreateAsync(); await test.SetState(ListingComplianceStatus.Approved, true);
        var user = await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer); var token = await test.App.TokenAsync(user);
        using var buyer = test.App.AuthenticatedClient(token);
        var state = await State(buyer); Assert.Empty(state.Lists); Assert.Equal(Guid.Empty, state.Revision);
        state = await Name(buyer, state, "Favourites"); var first = state.Lists.Single().Id;
        state = await Name(buyer, state, "Breeding pairs"); var second = state.Lists.Single(x => x.Id != first).Id;
        state = await Save(buyer, state, test.Id, [first, second]); Assert.All(state.Lists, x => Assert.Equal(test.Id, Assert.Single(x.ListingIds)));
        using (var fresh = test.App.AuthenticatedClient(token)) { var restored = await State(fresh); Assert.Equal(state.Revision, restored.Revision); Assert.Equal(2, restored.Lists.Count); }
        state = await Read(await buyer.PutAsJsonAsync($"/api/buyer/wishlists/{second}", new WishlistNameRequest(state.Revision, "For later")));
        Assert.Equal("For later", state.Lists.Single(x => x.Id == second).Name);
        state = await Save(buyer, state, test.Id, [second]); Assert.Empty(state.Lists.Single(x => x.Id == first).ListingIds);
        var page = await buyer.GetFromJsonAsync<WishlistLotsDto>($"/api/buyer/wishlists/{second}/lots"); Assert.Equal(test.Id, Assert.Single(page!.Items).Id); Assert.Equal(1, page.Total);
        state = await Save(buyer, state, test.Id, []); Assert.All(state.Lists, x => Assert.Empty(x.ListingIds));
        state = await Read(await buyer.DeleteAsync($"/api/buyer/wishlists/{first}?revision={state.Revision}")); Assert.Equal(second, Assert.Single(state.Lists).Id);
        using var scope = test.App.Server.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Listings.IgnoreQueryFilters().AnyAsync(x => x.Id == test.Id));
    }

    [Fact]
    public async Task Private_lists_reject_other_buyers_roles_tenants_and_forged_owner_fields()
    {
        await using var test = await FileCase.CreateAsync(); await test.SetState(ListingComplianceStatus.Approved, true);
        var user = await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer); using var buyer = test.App.AuthenticatedClient(await test.App.TokenAsync(user));
        var state = await Read(await buyer.PostAsJsonAsync("/api/buyer/wishlists", new { Revision = Guid.Empty, Name = "Private", BuyerId = Guid.NewGuid(), TenantId = Guid.NewGuid() }));
        var listId = state.Lists.Single().Id;
        using var other = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer)));
        Assert.Empty((await State(other)).Lists);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/buyer/wishlists/{listId}/lots")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/buyer/wishlists/{listId}", new WishlistNameRequest(Guid.Empty, "Steal"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PutAsJsonAsync($"/api/buyer/wishlists/items/{test.Id}", new WishlistMembershipRequest(Guid.Empty, [listId]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await test.Client.GetAsync("/api/buyer/wishlists")).StatusCode);
        using var admin = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(test.Tenant, FaunexRoles.TenantAdmin)));
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/buyer/wishlists")).StatusCode);
        using var platform = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin)));
        Assert.Equal(HttpStatusCode.Forbidden, (await platform.GetAsync("/api/buyer/wishlists")).StatusCode);
        var foreignTenant = await test.App.AddTenantAsync(); using var foreign = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(foreignTenant, FaunexRoles.Buyer)));
        Assert.Empty((await State(foreign)).Lists);
        var foreignState = await Name(foreign, await State(foreign), "Other tenant");
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.PutAsJsonAsync($"/api/buyer/wishlists/items/{test.Id}", new WishlistMembershipRequest(foreignState.Revision, [foreignState.Lists.Single().Id]))).StatusCode);
        test.App.Client.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader); test.App.Client.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(test.Tenant));
        Assert.Equal(HttpStatusCode.Unauthorized, (await test.App.Client.GetAsync("/api/buyer/wishlists")).StatusCode);
        using var scope = test.App.Server.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().BuyerWishlistCollections.IgnoreQueryFilters().AnyAsync(x => x.TenantId == test.Tenant && x.BuyerId == user.Id));
    }

    [Fact]
    public async Task Verified_domain_aliases_share_one_buyer_list_but_foreign_origins_cannot_read_it()
    {
        await using var test = await FileCase.CreateAsync();
        var user = await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer); var token = await test.App.TokenAsync(user);
        using var buyer = test.App.AuthenticatedClient(token); var state = await Name(buyer, await State(buyer), "Same tenant");
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); db.TenantDomains.Add(new TenantDomain { TenantId = test.Tenant, Hostname = "custom-wishlist.example.test", IsActive = true, VerifiedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync();
        }
        using var alias = test.App.AuthenticatedClient(token); alias.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader); alias.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, "custom-wishlist.example.test");
        Assert.Equal(state.Lists.Single().Id, (await State(alias)).Lists.Single().Id);
        var foreign = await test.App.AddTenantAsync(); alias.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader); alias.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, AccountApiFixture.TenantHost(foreign));
        Assert.Equal(HttpStatusCode.Forbidden, (await alias.GetAsync("/api/buyer/wishlists")).StatusCode);
    }

    [Fact]
    public async Task Withdrawn_or_unapproved_saved_lots_hide_ids_media_details_and_can_be_cleaned_without_deleting_lots()
    {
        await using var test = await FileCase.CreateAsync(); await test.SetState(ListingComplianceStatus.Approved, true);
        using var buyer = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer)));
        var state = await Name(buyer, await State(buyer), "Saved"); var list = state.Lists.Single().Id; state = await Save(buyer, state, test.Id, [list]);
        await test.SetState(ListingComplianceStatus.Suspended, true);
        var response = await buyer.GetAsync("/api/buyer/wishlists"); Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(test.Id.ToString(), json);
        state = (await response.Content.ReadFromJsonAsync<BuyerWishlistStateDto>())!; Assert.Empty(state.Lists.Single().ListingIds); Assert.Equal(1, state.Lists.Single().UnavailableCount);
        var lots = (await buyer.GetFromJsonAsync<WishlistLotsDto>($"/api/buyer/wishlists/{list}/lots"))!; Assert.Empty(lots.Items); Assert.Equal(0, lots.Total); Assert.Equal(1, lots.UnavailableCount);
        Assert.Equal(HttpStatusCode.NotFound, (await buyer.PutAsJsonAsync($"/api/buyer/wishlists/items/{test.Id}", new WishlistMembershipRequest(state.Revision, [list]))).StatusCode);
        state = await Read(await buyer.DeleteAsync($"/api/buyer/wishlists/{list}/unavailable?revision={state.Revision}")); Assert.Equal(0, state.Lists.Single().UnavailableCount);
        await test.SetState(ListingComplianceStatus.Approved, false);
        Assert.Equal(HttpStatusCode.NotFound, (await buyer.PutAsJsonAsync($"/api/buyer/wishlists/items/{test.Id}", new WishlistMembershipRequest(state.Revision, [list]))).StatusCode);
    }

    [Fact]
    public async Task Stale_revisions_never_overwrite_other_tab_changes()
    {
        await using var test = await FileCase.CreateAsync(); await test.SetState(ListingComplianceStatus.Approved, true);
        using var buyer = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer)));
        var original = await Name(buyer, await State(buyer), "First"); var list = original.Lists.Single().Id;
        var updated = await Name(buyer, original, "Second");
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.PostAsJsonAsync("/api/buyer/wishlists", new WishlistNameRequest(original.Revision, "Lost change"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.DeleteAsync($"/api/buyer/wishlists/{list}?revision={original.Revision}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await buyer.PutAsJsonAsync($"/api/buyer/wishlists/items/{test.Id}", new WishlistMembershipRequest(original.Revision, [list]))).StatusCode);
        Assert.Equal(updated.Revision, (await State(buyer)).Revision); Assert.Equal(2, (await State(buyer)).Lists.Count);
    }

    [Theory]
    [InlineData("")][InlineData("   ")][InlineData("bad\nname")]
    public async Task Names_are_validated(string name)
    {
        await using var test = await FileCase.CreateAsync(); using var buyer = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer)));
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PostAsJsonAsync("/api/buyer/wishlists", new WishlistNameRequest(Guid.Empty, name))).StatusCode);
        var state = await Name(buyer, await State(buyer), "  Favourites  "); Assert.Equal("Favourites", state.Lists.Single().Name);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PostAsJsonAsync("/api/buyer/wishlists", new WishlistNameRequest(state.Revision, "favourites"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PostAsJsonAsync("/api/buyer/wishlists", new WishlistNameRequest(state.Revision, new string('a', 81)))).StatusCode);
    }

    [Fact]
    public async Task Paged_saved_lots_use_public_projection_and_do_not_expose_private_fields()
    {
        await using var test = await FileCase.CreateAsync(); await test.SetState(ListingComplianceStatus.Approved, true);
        var user = await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer); using var buyer = test.App.AuthenticatedClient(await test.App.TokenAsync(user));
        var ids = await SeedLists(test, user.Id, 1, 61);
        var state = await State(buyer); var list = state.Lists.Single().Id;
        var response = await buyer.GetAsync($"/api/buyer/wishlists/{list}/lots?take=999"); var text = await response.Content.ReadAsStringAsync();
        var first = (await response.Content.ReadFromJsonAsync<WishlistLotsDto>())!; Assert.Equal(61, first.Total); Assert.Equal(48, first.Items.Count); Assert.Equal(ids.Last(), first.Items.First().Id);
        Assert.DoesNotContain("sellerId", text); Assert.DoesNotContain("bidderId", text); Assert.DoesNotContain("storageKey", text); Assert.DoesNotContain("reservePrice", text);
        var second = (await buyer.GetFromJsonAsync<WishlistLotsDto>($"/api/buyer/wishlists/{list}/lots?skip=48&take=48"))!; Assert.Equal(13, second.Items.Count); Assert.Empty(first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)));
    }

    [Fact]
    public async Task List_and_membership_limits_are_atomic()
    {
        await using var test = await FileCase.CreateAsync(); await test.SetState(ListingComplianceStatus.Approved, true);
        var user = await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer); using var buyer = test.App.AuthenticatedClient(await test.App.TokenAsync(user));
        await SeedLists(test, user.Id, 20, 50); var state = await State(buyer);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PostAsJsonAsync("/api/buyer/wishlists", new WishlistNameRequest(state.Revision, "Too many"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PutAsJsonAsync($"/api/buyer/wishlists/items/{test.Id}", new WishlistMembershipRequest(state.Revision, state.Lists.Select(x => x.Id).ToArray()))).StatusCode);
        Assert.Equal(state.Revision, (await State(buyer)).Revision); Assert.Equal(1000, (await State(buyer)).Lists.Sum(x => x.ListingIds.Count));
    }

    [PostgresFact]
    public async Task Concurrent_initial_creation_and_renames_preserve_one_revision_and_owner()
    {
        await using var test = await FileCase.CreateAsync(); var user = await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer); var token = await test.App.TokenAsync(user);
        using var a = test.App.AuthenticatedClient(token); using var b = test.App.AuthenticatedClient(token);
        var creates = await Task.WhenAll(a.PostAsJsonAsync("/api/buyer/wishlists", new WishlistNameRequest(Guid.Empty, "First")), b.PostAsJsonAsync("/api/buyer/wishlists", new WishlistNameRequest(Guid.Empty, "Second")));
        Assert.Single(creates, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(creates, x => x.StatusCode == HttpStatusCode.Conflict);
        var state = await State(a); var list = state.Lists.Single().Id;
        var renames = await Task.WhenAll(a.PutAsJsonAsync($"/api/buyer/wishlists/{list}", new WishlistNameRequest(state.Revision, "A")), b.PutAsJsonAsync($"/api/buyer/wishlists/{list}", new WishlistNameRequest(state.Revision, "B")));
        Assert.Single(renames, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(renames, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains((await State(a)).Lists.Single().Name, new[] { "A", "B" });
        foreach (var response in creates.Concat(renames)) response.Dispose();
    }
    [Fact]
    public async Task A_full_list_and_duplicate_target_ids_are_rejected_without_partial_changes()
    {
        await using var test = await FileCase.CreateAsync(); await test.SetState(ListingComplianceStatus.Approved, true);
        var user = await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer); using var buyer = test.App.AuthenticatedClient(await test.App.TokenAsync(user));
        await SeedLists(test, user.Id, 1, 200); var state = await State(buyer); var id = state.Lists.Single().Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PutAsJsonAsync($"/api/buyer/wishlists/items/{test.Id}", new WishlistMembershipRequest(state.Revision, [id]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyer.PutAsJsonAsync($"/api/buyer/wishlists/items/{test.Id}", new WishlistMembershipRequest(state.Revision, [id, id]))).StatusCode);
        Assert.Equal(state.Revision, (await State(buyer)).Revision); Assert.Equal(200, (await State(buyer)).Lists.Single().ListingIds.Count);
    }
    private static async Task<List<Guid>> SeedLists(FileCase test, Guid buyer, int count, int items)
    {
        using var scope = test.App.Server.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seller = await db.Listings.IgnoreQueryFilters().Where(x => x.Id == test.Id).Select(x => x.SellerId).SingleAsync();
        var rows = Enumerable.Range(0, items).Select(i => new Listing { TenantId = test.Tenant, SellerId = seller, Title = $"TEST ONLY {i}", StartingPrice = 100, CurrencyCode = "ZAR", IsActive = true, Quantity = 1 }).ToList();
        foreach (var row in rows) row.Compliance = new() { TenantId = test.Tenant, ListingId = row.Id, Status = ListingComplianceStatus.Approved };
        db.Listings.AddRange(rows); var ids = rows.Select(x => x.Id).ToList();
        db.BuyerWishlistCollections.Add(new() { TenantId = test.Tenant, BuyerId = buyer, Revision = Guid.NewGuid(), ContentsJson = JsonSerializer.Serialize(Enumerable.Range(0, count).Select(i => new { Id = Guid.NewGuid(), Name = $"List {i}", ListingIds = ids })) });
        await db.SaveChangesAsync(); return ids;
    }
    private static async Task<BuyerWishlistStateDto> State(HttpClient client) => (await client.GetFromJsonAsync<BuyerWishlistStateDto>("/api/buyer/wishlists"))!;
    private static Task<BuyerWishlistStateDto> Name(HttpClient client, BuyerWishlistStateDto state, string name) => Read(client.PostAsJsonAsync("/api/buyer/wishlists", new WishlistNameRequest(state.Revision, name)));
    private static Task<BuyerWishlistStateDto> Save(HttpClient client, BuyerWishlistStateDto state, Guid listing, IReadOnlyList<Guid> lists) => Read(client.PutAsJsonAsync($"/api/buyer/wishlists/items/{listing}", new WishlistMembershipRequest(state.Revision, lists)));
    private static async Task<BuyerWishlistStateDto> Read(Task<HttpResponseMessage> response) => await Read(await response);
    private static async Task<BuyerWishlistStateDto> Read(HttpResponseMessage response) { using (response) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<BuyerWishlistStateDto>())!; } }
}
