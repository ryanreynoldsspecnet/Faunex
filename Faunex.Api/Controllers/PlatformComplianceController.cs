using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using Faunex.Api.Compliance;
using Faunex.Application.DTOs;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/platform/compliance")]
[Authorize(Policy = "PlatformCompliance")]
public sealed class PlatformComplianceController(IBirdListingService birdListings, ListingReviewQueries queries) : ControllerBase
{
    public sealed record ReviewListingRequest([Required] Guid? Revision, [StringLength(2000)] string? Notes);

    [HttpGet("listings")]
    public Task<ReviewQueueResult> Queue([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int skip = 0, [FromQuery] int take = 25, CancellationToken cancellationToken = default) =>
        queries.QueueAsync(search, status, skip, take, cancellationToken);

    [HttpGet("listings/{listingId:guid}")]
    public Task<ListingReviewDto> Detail(Guid listingId, CancellationToken cancellationToken) => queries.DetailAsync(listingId, cancellationToken);

    [HttpPost("listings/{listingId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid listingId, [FromBody] ReviewListingRequest request, CancellationToken cancellationToken)
    {
        await birdListings.ApproveListingAsync(listingId, request.Notes, cancellationToken, request.Revision);
        return NoContent();
    }

    [HttpPost("listings/{listingId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid listingId, [FromBody] ReviewListingRequest request, CancellationToken cancellationToken)
    {
        await birdListings.RejectListingAsync(listingId, request.Notes, cancellationToken, request.Revision);
        return NoContent();
    }

    [HttpPost("listings/{listingId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid listingId, [FromBody] ReviewListingRequest request, CancellationToken cancellationToken)
    {
        await birdListings.SuspendListingAsync(listingId, request.Notes, cancellationToken, request.Revision);
        return NoContent();
    }
}
