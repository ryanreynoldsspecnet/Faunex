using Faunex.Api.Files;
using Faunex.Application.DTOs;
using Faunex.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Faunex.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/listings/{listingId:guid}/files")]
public sealed class ListingFilesController(ListingFileService files) : ControllerBase
{
    [HttpGet]
    public Task<ListingFilesDto> List(Guid listingId, CancellationToken ct) => files.ListAsync(listingId, ct);

    [HttpPost]
    [Authorize(Policy = "ListingManager")]
    [EnableRateLimiting("file-uploads")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public Task<ListingFilesDto> Upload(Guid listingId, [FromForm] UploadListingFileRequest request, CancellationToken ct) =>
        files.UploadAsync(listingId, request.Revision, request.File, request.IsPhoto, request.DocumentType, ct);

    [HttpDelete("{fileId:guid}")]
    [Authorize(Policy = "ListingManager")]
    public Task<ListingFilesDto> Delete(Guid listingId, Guid fileId, [FromQuery, BindRequired] Guid revision, CancellationToken ct) => files.DeleteAsync(listingId, fileId, revision, ct);

    [HttpGet("{fileId:guid}/thumbnail")]
    [EnableRateLimiting("file-uploads")]
    public async Task<IActionResult> Thumbnail(Guid listingId, Guid fileId, CancellationToken ct)
    {
        var file = await files.ReadThumbnailAsync(listingId, fileId, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Bytes, file.ContentType);
    }

    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> Download(Guid listingId, Guid fileId, CancellationToken ct)
    {
        var file = await files.ReadPrivateAsync(listingId, fileId, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";
        return File(file.Bytes, file.ContentType, file.FileName);
    }
}

public sealed class UploadListingFileRequest
{
    public IFormFile File { get; set; } = default!;
    [BindRequired] public Guid Revision { get; set; }
    public bool IsPhoto { get; set; }
    public ListingDocumentType? DocumentType { get; set; }
}

[ApiController]
[Route("api/marketplace")]
public sealed class MarketplacePhotosController(ListingFileService files) : ControllerBase
{
    [HttpGet("listings/{listingId:guid}/photos")]
    public Task<IReadOnlyList<Guid>> Photos(Guid listingId, CancellationToken ct) => files.PublicPhotosAsync(listingId, ct);
    [HttpGet("photos/{fileId:guid}")]
    public async Task<IActionResult> Photo(Guid fileId, CancellationToken ct)
    {
        var file = await files.ReadPublicPhotoAsync(fileId, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Bytes, file.ContentType);
    }
}
