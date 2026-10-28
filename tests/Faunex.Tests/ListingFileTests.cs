using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Faunex.Api.Files;
using Faunex.Application.Auth;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Faunex.Tests;

public sealed class ListingFileTests
{
    [Fact]
    public async Task Photo_upload_is_private_until_approved_and_never_exposes_storage_keys()
    {
        await using var test = await FileCase.CreateAsync();
        var result = await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, Png(), "bird.png", "image/png");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var snapshot = (await result.Content.ReadFromJsonAsync<ListingFilesDto>())!;
        var file = Assert.Single(snapshot.Files);
        Assert.True(file.IsPhoto);
        Assert.DoesNotContain("storageKey", await result.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var download = await test.Client.GetAsync($"{test.Path}/{file.Id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.True(download.Headers.CacheControl!.NoStore);
        var thumbnail = await test.Client.GetAsync($"{test.Path}/{file.Id}/thumbnail");
        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.Equal("image/jpeg", thumbnail.Content.Headers.ContentType!.MediaType);
        Assert.True((await thumbnail.Content.ReadAsByteArrayAsync()).Length < 150 * 1024);
        Assert.Equal(HttpStatusCode.Unauthorized, (await test.App.Client.GetAsync($"{test.Path}/{file.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await test.App.Client.GetAsync($"/api/marketplace/photos/{file.Id}")).StatusCode);
        await test.SetState(ListingComplianceStatus.Approved, true);
        Assert.Equal(HttpStatusCode.OK, (await test.App.Client.GetAsync($"/api/marketplace/photos/{file.Id}")).StatusCode);
        var photos = await test.App.Client.GetFromJsonAsync<List<Guid>>($"/api/marketplace/listings/{test.Id}/photos");
        Assert.Equal(file.Id, Assert.Single(photos!));
        await test.SetState(ListingComplianceStatus.Draft, false);
        Assert.Equal(HttpStatusCode.NotFound, (await test.App.Client.GetAsync($"/api/marketplace/photos/{file.Id}")).StatusCode);
    }

    [Fact]
    public async Task Documents_are_never_public_and_private_access_respects_tenant_owner_and_reviewer_roles()
    {
        await using var test = await FileCase.CreateAsync();
        var result = await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, Pdf(), "permit.pdf", "application/pdf", false);
        var file = Assert.Single((await result.Content.ReadFromJsonAsync<ListingFilesDto>())!.Files);
        await test.SetState(ListingComplianceStatus.Approved, true);
        Assert.Equal(HttpStatusCode.NotFound, (await test.App.Client.GetAsync($"/api/marketplace/photos/{file.Id}")).StatusCode);
        using var buyer = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(test.Tenant, FaunexRoles.Buyer)));
        using var other = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(test.Tenant, FaunexRoles.Seller)));
        using var platform = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(null, FaunexRoles.PlatformAdmin)));
        using var reviewer = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(null, FaunexRoles.PlatformComplianceAdmin)));
        foreach (var client in new[] { buyer, other, platform })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{test.Path}/{file.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.GetAsync($"{test.Path}/{file.Id}")).StatusCode);
        using var foreign = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(await test.App.AddTenantAsync(), FaunexRoles.TenantAdmin)));
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync(test.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"{test.Path}/{file.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.DeleteAsync($"{test.Path}/{file.Id}?revision={(await test.Snapshot()).Revision}")).StatusCode);
    }

    [Theory]
    [InlineData("bad.svg", "image/svg+xml", "<svg onload='alert(1)'/>")]
    [InlineData("fake.png", "image/png", "not an image")]
    [InlineData("file.exe", "image/png", "not an image")]
    [InlineData("../bird.png", "image/png", "not an image")]
    [InlineData("C:\\bird.png", "image/png", "not an image")]
    public async Task Unsafe_or_mislabeled_files_are_rejected_without_storage(string name, string type, string body)
    {
        await using var test = await FileCase.CreateAsync();
        var response = await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, System.Text.Encoding.UTF8.GetBytes(body), name, type);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(test.Store.Files);
        Assert.Empty((await test.Snapshot()).Files);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejected_or_unavailable_scans_fail_closed(bool unavailable)
    {
        await using var test = await FileCase.CreateAsync();
        var scanner = test.App.Server.Services.GetRequiredService<TestUploadScanner>();
        scanner.Reject = !unavailable; scanner.Unavailable = unavailable;
        var response = await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, Png(), "bird.png", "image/png");
        Assert.Equal(unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(test.Store.Files);
        Assert.Empty((await test.Snapshot()).Files);
    }

    [Fact]
    public async Task Stale_revision_after_scan_cannot_write_an_orphan_or_overwrite_changed_listing()
    {
        await using var test = await FileCase.CreateAsync();
        var snapshot = await test.Snapshot();
        var dto = (await test.Client.GetFromJsonAsync<BirdListingDto>($"/api/birdlistings/{test.Id}"))!;
        test.App.Server.Services.GetRequiredService<TestUploadScanner>().Handler = async () =>
            Assert.Equal(HttpStatusCode.NoContent, (await test.Client.PutAsJsonAsync($"/api/birdlistings/{test.Id}", dto with { Title = "Changed during scan" })).StatusCode);
        var response = await UploadAsync(test.Client, test.Id, snapshot.Revision, Png(), "bird.png", "image/png");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(test.Store.Files);
    }

    [Fact]
    public async Task Delete_invalidates_approval_revokes_content_and_removes_bytes()
    {
        await using var test = await FileCase.CreateAsync();
        var response = await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, Png(), "bird.png", "image/png");
        var uploaded = (await response.Content.ReadFromJsonAsync<ListingFilesDto>())!;
        var file = Assert.Single(uploaded.Files);
        await test.SetState(ListingComplianceStatus.Approved, true);
        var current = await test.Snapshot();
        Assert.Equal(HttpStatusCode.OK, (await test.Client.DeleteAsync($"{test.Path}/{file.Id}?revision={current.Revision}")).StatusCode);
        Assert.Empty(test.Store.Files);
        Assert.Equal("Draft", (await test.Snapshot()).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await test.Client.GetAsync($"{test.Path}/{file.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await test.App.Client.GetAsync($"/api/marketplace/photos/{file.Id}")).StatusCode);
    }

    [Fact]
    public async Task Only_managed_intact_evidence_satisfies_required_documents_and_review_changes_are_rechecked()
    {
        await using var test = await FileCase.CreateAsync();
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ListingDocumentRequirements.Add(new ListingDocumentRequirement { TenantId = test.Tenant, AnimalClass = "bird", DocumentType = ListingDocumentType.CitesPermit });
            db.ListingDocuments.Add(new ListingDocument { ListingId = test.Id, TenantId = test.Tenant, DocumentType = ListingDocumentType.CitesPermit, UploadedAt = DateTimeOffset.UtcNow, FileUrl = "https://example.test/not-proof.pdf" });
            await db.SaveChangesAsync();
        }
        await test.Client.PostAsJsonAsync($"/api/seller/listings/{test.Id}/submit", new { });
        Assert.Equal("PendingDocuments", (await test.Snapshot()).Status);
        var response = await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, Pdf(), "permit.pdf", "application/pdf", false);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await test.Client.PostAsJsonAsync($"/api/seller/listings/{test.Id}/submit", new { });
        Assert.Equal("UnderReview", (await test.Snapshot()).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, Png(), "late.png", "image/png")).StatusCode);
        var key = Assert.Single(test.Store.Files).Key;
        test.Store.Files.TryRemove(key, out _);
        using var reviewer = test.App.AuthenticatedClient(await test.App.TokenAsync(await test.App.AddUserAsync(null, FaunexRoles.PlatformSuperAdmin)));
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync($"/api/platform/compliance/listings/{test.Id}/approve", new { Notes = "Reviewed", Revision = (await test.Snapshot()).Revision })).StatusCode);
        Assert.Equal("UnderReview", (await test.Snapshot()).Status);
    }

    [Fact]
    public async Task Corrupt_content_is_not_returned_and_duplicate_names_do_not_overwrite()
    {
        await using var test = await FileCase.CreateAsync();
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, Png(), "same.png", "image/png")).StatusCode);
        Assert.Equal(2, test.Store.Files.Count);
        var snapshot = await test.Snapshot();
        using var scope = test.App.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.ListingFiles.IgnoreQueryFilters().SingleAsync(x => x.Id == snapshot.Files[0].Id);
        test.Store.Files[stored.StorageKey] = [1, 2, 3];
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await test.Client.GetAsync($"{test.Path}/{stored.Id}")).StatusCode);
    }

    [Fact]
    public async Task Photos_are_normalized_and_reoriented_without_Exif()
    {
        await using var test = await FileCase.CreateAsync();
        using var bitmap = new SKBitmap(32, 24); bitmap.Erase(SKColors.Green);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        byte[] exif = [0xff,0xe1,0,34,69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,6,0,0,0,0,0,0,0];
        var original = jpeg.ToArray();
        var oriented = original[..2].Concat(exif).Concat(original[2..]).ToArray();
        var response = await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, oriented, "rotated.jpg", "image/jpeg");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var file = Assert.Single((await response.Content.ReadFromJsonAsync<ListingFilesDto>())!.Files);
        var bytes = await test.Client.GetByteArrayAsync($"{test.Path}/{file.Id}");
        using var data = SKData.CreateCopy(bytes); using var codec = SKCodec.Create(data);
        Assert.Equal(24, codec.Info.Width); Assert.Equal(32, codec.Info.Height);
        Assert.Equal(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
    }

    [Fact]
    public async Task Pixel_limit_and_photo_quota_are_enforced()
    {
        await using var test = await FileCase.CreateAsync();
        using (var bitmap = new SKBitmap(5000, 4000))
        {
            bitmap.Erase(SKColors.Red); using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, png.ToArray(), "huge.png", "image/png")).StatusCode);
        }
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (var i = 0; i < 10; i++) db.ListingFiles.Add(new ListingFile { TenantId = test.Tenant, ListingId = test.Id, IsPhoto = true, StorageKey = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, Png(), "extra.png", "image/png")).StatusCode);
        Assert.Empty(test.Store.Files);
    }

    internal static byte[] Png()
    {
        using var bitmap = new SKBitmap(40, 30); bitmap.Erase(SKColors.ForestGreen);
        using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    [Theory]
    [InlineData(ListingComplianceStatus.UnderReview, false)]
    [InlineData(ListingComplianceStatus.Suspended, false)]
    [InlineData(ListingComplianceStatus.Draft, true)]
    public async Task Review_suspension_and_existing_auctions_lock_upload_and_delete(ListingComplianceStatus status, bool auction)
    {
        await using var test = await FileCase.CreateAsync();
        var response = await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, Png(), "bird.png", "image/png");
        var file = Assert.Single((await response.Content.ReadFromJsonAsync<ListingFilesDto>())!.Files);
        await test.SetState(status, false);
        if (auction)
        {
            using var scope = test.App.Server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Auctions.Add(new Auction { TenantId = test.Tenant, ListingId = test.Id, IsClosed = true });
            await db.SaveChangesAsync();
        }
        var snapshot = await test.Snapshot();
        Assert.False(snapshot.CanModify);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(test.Client, test.Id, snapshot.Revision, Png(), "extra.png", "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await test.Client.DeleteAsync($"{test.Path}/{file.Id}?revision={snapshot.Revision}")).StatusCode);
        Assert.Single(test.Store.Files);
    }

    [Fact]
    public async Task Missing_revision_empty_file_mislabeled_image_and_tenant_quota_are_rejected()
    {
        await using var test = await FileCase.CreateAsync();
        var revision = (await test.Snapshot()).Revision;
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(test.Client, test.Id, revision, [], "empty.png", "image/png")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(test.Client, test.Id, revision, Png(), "bird.jpg", "image/jpeg")).StatusCode);
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new ByteArrayContent(Png()), "File", "bird.png");
            form.Add(new StringContent("true"), "IsPhoto");
            Assert.Equal(HttpStatusCode.BadRequest, (await test.Client.PostAsync(test.Path, form)).StatusCode);
        }
        using (var scope = test.App.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ListingFiles.Add(new ListingFile { TenantId = test.Tenant, ListingId = test.Id, Length = FileUploadOptions.MaxTenantBytes, StorageKey = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(test.Client, test.Id, revision, Png(), "bird.png", "image/png")).StatusCode);
        Assert.Empty(test.Store.Files);
    }

    [PostgresFact]
    public async Task Concurrent_uploads_of_one_revision_commit_only_one_file_and_clean_the_loser()
    {
        await using var test = await FileCase.CreateAsync();
        var revision = (await test.Snapshot()).Revision;
        var results = await Task.WhenAll(
            UploadAsync(test.Client, test.Id, revision, Png(), "one.png", "image/png"),
            UploadAsync(test.Client, test.Id, revision, Png(), "two.png", "image/png"));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Single((await test.Snapshot()).Files);
        Assert.Single(test.Store.Files);
        foreach (var response in results) response.Dispose();
    }

    [ClamAvFact]
    public async Task Real_scanner_is_used_by_the_upload_pipeline_for_clean_photos_and_documents()
    {
        await using var test = await FileCase.CreateAsync();
        var parts = Environment.GetEnvironmentVariable("FAUNEX_TEST_CLAMAV")!.Split(':');
        var scanner = new ClamAvUploadScanner(Microsoft.Extensions.Options.Options.Create(new FileUploadOptions { ScannerHost = parts[0], ScannerPort = int.Parse(parts[1]) }));
        byte[] bytes = Png();
        test.App.Server.Services.GetRequiredService<TestUploadScanner>().Handler = () => scanner.ScanAsync(bytes, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, bytes, "bird.png", "image/png")).StatusCode);
        bytes = Pdf();
        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(test.Client, test.Id, (await test.Snapshot()).Revision, bytes, "permit.pdf", "application/pdf", false)).StatusCode);
        Assert.Equal(2, test.Store.Files.Count);
    }
    internal static byte[] Pdf() => "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\n%%EOF\n"u8.ToArray();
    internal static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid id, Guid revision, byte[] bytes, string name, string type, bool photo = true)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes); content.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(content, "File", name); form.Add(new StringContent(revision.ToString()), "Revision");
        form.Add(new StringContent(photo ? "true" : "false"), "IsPhoto");
        if (!photo) form.Add(new StringContent(((int)ListingDocumentType.CitesPermit).ToString()), "DocumentType");
        return await client.PostAsync($"/api/listings/{id}/files", form);
    }
}

internal sealed class FileCase : IAsyncDisposable
{
    public AccountApiFixture App { get; private init; } = null!;
    public HttpClient Client { get; private init; } = null!;
    public Guid Id { get; private init; }
    public Guid Tenant { get; private init; }
    public string Path => $"/api/listings/{Id}/files";
    public TestFileStorage Store => App.Server.Services.GetRequiredService<TestFileStorage>();
    public async Task<ListingFilesDto> Snapshot() => (await Client.GetFromJsonAsync<ListingFilesDto>(Path))!;
    public static async Task<FileCase> CreateAsync()
    {
        var app = await AccountApiFixture.CreateAsync();
        var tenant = await app.AddTenantAsync();
        var user = await app.AddUserAsync(tenant, FaunexRoles.Seller);
        var client = app.AuthenticatedClient(await app.TokenAsync(user));
        using var scope = app.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var species = new BirdSpecies { CommonName = "Test bird", ScientificName = "Testus birdus" };
        db.Add(species); await db.SaveChangesAsync();
        var response = await client.PostAsJsonAsync("/api/seller/listings/bird", new CreateBirdListingRequest(species.Id, "File test bird", null, 100m, true, user.Id));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<ListingDto>())!.Id;
        return new() { App = app, Client = client, Tenant = tenant, Id = id };
    }
    public async Task SetState(ListingComplianceStatus status, bool active)
    {
        using var scope = App.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var listing = await db.Listings.IgnoreQueryFilters().Include(x => x.Compliance).SingleAsync(x => x.Id == Id);
        listing.Compliance!.Status = status; listing.IsActive = active; listing.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }
    public async ValueTask DisposeAsync() { Client.Dispose(); await App.DisposeAsync(); }
}
