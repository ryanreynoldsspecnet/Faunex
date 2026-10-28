using System.Net;
using System.Net.Http.Json;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class ListingReviewWorkflowTests
{
    [Fact]
    public async Task Reject_edit_resubmit_approve_and_suspend_preserve_history_and_private_evidence()
    {
        await using var test = await FileCase.CreateAsync();
        using var reviewer = await Reviewer(test);
        var uploaded = await ListingFileTests.UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, ListingFileTests.Png(), "bird.png", "image/png");
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var photo = Assert.Single((await uploaded.Content.ReadFromJsonAsync<ListingFilesDto>())!.Files);
        await Submit(test);
        var first = await Detail(test, reviewer);
        Assert.Equal("UnderReview", first.Status);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.GetAsync($"{test.Path}/{photo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(test, reviewer, "reject", first.Revision, "Please correct the ring number.")).StatusCode);
        var rejected = await OwnerDetail(test);
        Assert.Equal("Rejected", rejected.Status); Assert.True(rejected.CanSubmit);
        var edit = (await test.Client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{test.Id}"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await test.Client.PutAsJsonAsync($"/api/birdlistings/{test.Id}", edit with { RingNumber = "ZA-CORRECTED" })).StatusCode);
        await Submit(test);
        var next = await Detail(test, reviewer);
        Assert.Equal("ZA-CORRECTED", next.RingNumber);
        Assert.NotEqual(first.Revision, next.Revision);
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(test, reviewer, "approve", next.Revision, "Reviewed supplied details.")).StatusCode);
        var approved = await Detail(test, reviewer);
        Assert.Equal("Approved", approved.Status); Assert.True(approved.Listing.IsActive);
        Assert.Equal(HttpStatusCode.OK, (await test.App.Client.GetAsync($"/api/marketplace/photos/{photo.Id}")).StatusCode);
        Assert.Contains(approved.History, x => x.ToStatus == "Rejected" && x.Notes == "Please correct the ring number.");
        Assert.Equal(2, approved.History.Count(x => x.ToStatus == "UnderReview"));
        Assert.Contains(approved.History, x => x.ToStatus == "Approved" && x.ActorKind == "Platform reviewer" && x.ActorId.HasValue && x.Revision == approved.Revision);
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(test, reviewer, "suspend", approved.Revision, "Additional evidence needs checking.")).StatusCode);
        var suspended = await Detail(test, reviewer);
        Assert.False(suspended.CanSubmit); Assert.False(suspended.Listing.IsActive);
        Assert.Equal(HttpStatusCode.NotFound, (await test.App.Client.GetAsync($"/api/marketplace/photos/{photo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(test, reviewer, "reject", suspended.Revision, "Please supply an updated certificate.")).StatusCode);
        Assert.True((await OwnerDetail(test)).CanSubmit);
    }

    [Fact]
    public async Task Decisions_require_current_revision_valid_state_and_reason_and_do_not_record_failed_attempts()
    {
        await using var test = await FileCase.CreateAsync();
        using var reviewer = await Reviewer(test);
        var initial = await Detail(test, reviewer);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync($"/api/platform/compliance/listings/{test.Id}/approve", new { Notes = "Missing revision" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(test, reviewer, "approve", initial.Revision, null)).StatusCode);
        await Submit(test);
        var current = await Detail(test, reviewer);
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(test, reviewer, "approve", initial.Revision, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Decide(test, reviewer, "reject", current.Revision, "  ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Decide(test, reviewer, "approve", current.Revision, new string('x', 2001))).StatusCode);
        Assert.Equal(current.History.Count, (await Detail(test, reviewer)).History.Count);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Client.PostAsJsonAsync($"/api/seller/listings/{test.Id}/submit", new { current.Revision })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(test, reviewer, "approve", current.Revision, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(test, reviewer, "approve", current.Revision, null)).StatusCode);
        Assert.Single((await Detail(test, reviewer)).History, x => x.ToStatus == "Approved");
    }

    [Fact]
    public async Task Missing_optional_file_or_new_required_policy_blocks_approval()
    {
        await using var test = await FileCase.CreateAsync();
        using var reviewer = await Reviewer(test);
        await ListingFileTests.UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, ListingFileTests.Pdf(), "permit.pdf", "application/pdf", false);
        await Submit(test);
        var current = await Detail(test, reviewer);
        var entry = Assert.Single(test.Store.Files);
        test.Store.Files.TryRemove(entry.Key, out _);
        Assert.Equal(HttpStatusCode.BadRequest, (await Decide(test, reviewer, "approve", current.Revision, null)).StatusCode);
        test.Store.Files[entry.Key] = entry.Value;
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ListingDocumentRequirements.Add(new ListingDocumentRequirement { TenantId = test.Tenant, AnimalClass = "bird", DocumentType = ListingDocumentType.VeterinaryCertificate });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Decide(test, reviewer, "approve", current.Revision, null)).StatusCode);
        Assert.Equal("UnderReview", (await OwnerDetail(test)).Status);
    }

    [Fact]
    public async Task Required_managed_document_workflow_and_actor_history_are_persisted()
    {
        await using var test = await FileCase.CreateAsync();
        using var reviewer = await Reviewer(test);
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ListingDocumentRequirements.Add(new ListingDocumentRequirement { TenantId = test.Tenant, AnimalClass = "bird", DocumentType = ListingDocumentType.CitesPermit });
            await db.SaveChangesAsync();
        }
        await Submit(test);
        Assert.Equal("PendingDocuments", (await OwnerDetail(test)).Status);
        await ListingFileTests.UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, ListingFileTests.Pdf(), "permit.pdf", "application/pdf", false);
        await Submit(test);
        var current = await Detail(test, reviewer);
        Assert.Equal(HttpStatusCode.NoContent, (await Decide(test, reviewer, "approve", current.Revision, "Evidence inspected.")).StatusCode);
        var final = await OwnerDetail(test);
        Assert.Contains(final.History, x => x.ToStatus == "PendingDocuments" && x.ActorKind == "Tenant user" && x.ActorId == final.Listing.SellerId);
        Assert.Contains(final.History, x => x.ToStatus == "Approved" && x.Notes == "Evidence inspected.");
        using var scope2 = test.App.Server.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recorded = await db2.ListingReviewEvents.IgnoreQueryFilters().FirstAsync(x => x.ListingId == test.Id);
        recorded.Notes = "Attempted overwrite";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db2.SaveChangesAsync());
        Assert.DoesNotContain((await OwnerDetail(test)).History, x => x.Notes == "Attempted overwrite");
    }

    [Fact]
    public async Task History_and_decisions_are_denied_to_foreign_tenants_other_sellers_buyers_and_general_platform_staff()
    {
        await using var test = await FileCase.CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await test.App.Client.GetAsync($"/api/listings/{test.Id}/review")).StatusCode);
        foreach (var role in new[] { FaunexRoles.Buyer, FaunexRoles.Seller, FaunexRoles.PlatformAdmin, FaunexRoles.PlatformSupport })
        {
            var platform = role.StartsWith("Platform", StringComparison.Ordinal);
            using var client = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(platform ? null : test.Tenant, role)));
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/listings/{test.Id}/review")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/platform/compliance/listings")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Decide(test, client, "approve", (await test.Snapshot()).Revision, null)).StatusCode);
        }
        using var foreign = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(await test.App.AddTenantAsync(), FaunexRoles.TenantAdmin)));
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/listings/{test.Id}/review")).StatusCode);
        using var admin = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(test.Tenant, FaunexRoles.TenantAdmin)));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/listings/{test.Id}/review")).StatusCode);
    }

    [PostgresFact]
    public async Task Concurrent_reviewers_cannot_record_conflicting_decisions()
    {
        await using var test = await FileCase.CreateAsync();
        using var first = await Reviewer(test); using var second = await Reviewer(test);
        await Submit(test);
        var revision = (await Detail(test, first)).Revision;
        var responses = await Task.WhenAll(Decide(test, first, "approve", revision, "Accepted"), Decide(test, second, "reject", revision, "Changes needed"));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var result = await Detail(test, first);
        Assert.Single(result.History, x => x.ToStatus is "Approved" or "Rejected");
        Assert.Equal(result.Status == "Approved", result.Listing.IsActive);
        foreach (var response in responses) response.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inactive_tenants_and_existing_auctions_block_review_decisions(bool auction)
    {
        await using var test = await FileCase.CreateAsync();
        using var reviewer = await Reviewer(test);
        await Submit(test);
        var current = await Detail(test, reviewer);
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (auction) db.Auctions.Add(new Auction { TenantId = test.Tenant, ListingId = test.Id, IsClosed = true });
            else (await db.Tenants.SingleAsync(x => x.Id == test.Tenant)).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.False((await Detail(test, reviewer)).CanReview);
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(test, reviewer, "approve", current.Revision, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(test, reviewer, "reject", current.Revision, "Changes needed")).StatusCode);
        Assert.Equal(current.History.Count, (await Detail(test, reviewer)).History.Count);
    }

    [PostgresFact]
    public async Task Queue_is_status_filtered_searchable_paginated_and_invalid_filters_rejected()
    {
        await using var test = await FileCase.CreateAsync();
        using var reviewer = await Reviewer(test);
        var seller = (await OwnerDetail(test)).Listing.SellerId!.Value;
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (var i = 0; i < 27; i++) db.Listings.Add(new Listing { TenantId = test.Tenant, SellerId = seller, Title = $"Queue fixture {i:00}", IsActive = false,
                Compliance = new ListingCompliance { TenantId = test.Tenant, Status = ListingComplianceStatus.UnderReview, SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(i) } });
            await db.SaveChangesAsync();
        }
        var page = (await reviewer.GetFromJsonAsync<ReviewQueueResult>("/api/platform/compliance/listings?search=queue%20fixture&take=25"))!;
        Assert.Equal(27, page.Total); Assert.Equal(25, page.Items.Count);
        var next = (await reviewer.GetFromJsonAsync<ReviewQueueResult>("/api/platform/compliance/listings?search=queue%20fixture&skip=25&take=25"))!;
        Assert.Equal(2, next.Items.Count); Assert.Empty(page.Items.Select(x => x.Id).Intersect(next.Items.Select(x => x.Id)));
        Assert.Empty((await reviewer.GetFromJsonAsync<ReviewQueueResult>("/api/platform/compliance/listings?status=Approved"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.GetAsync("/api/platform/compliance/listings?status=Invalid")).StatusCode);
    }

    private static async Task<HttpClient> Reviewer(FileCase test) => test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(null, FaunexRoles.PlatformComplianceAdmin)));
    private static async Task<ListingReviewDto> Detail(FileCase test, HttpClient reviewer) => (await reviewer.GetFromJsonAsync<ListingReviewDto>($"/api/platform/compliance/listings/{test.Id}"))!;
    private static async Task<ListingReviewDto> OwnerDetail(FileCase test) => (await test.Client.GetFromJsonAsync<ListingReviewDto>($"/api/listings/{test.Id}/review"))!;
    private static async Task Submit(FileCase test) => Assert.Equal(HttpStatusCode.NoContent, (await test.Client.PostAsJsonAsync($"/api/seller/listings/{test.Id}/submit", new { Revision = (await OwnerDetail(test)).Revision })).StatusCode);
    private static Task<HttpResponseMessage> Decide(FileCase test, HttpClient client, string action, Guid revision, string? notes) => client.PostAsJsonAsync($"/api/platform/compliance/listings/{test.Id}/{action}", new { Revision = revision, Notes = notes });
}
