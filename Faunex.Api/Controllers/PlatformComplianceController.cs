using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/platform/compliance")]
[Authorize(Policy = "PlatformCompliance")]
public sealed class PlatformComplianceController(IBirdListingService birdListings) : ControllerBase
{
    public sealed record ReviewListingRequest(string? Notes);

    [HttpPost("listings/{listingId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid listingId, [FromBody] ReviewListingRequest request, CancellationToken cancellationToken)
    {
        await birdListings.ApproveListingAsync(listingId, request?.Notes, cancellationToken);
        return NoContent();
    }

    [HttpPost("listings/{listingId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid listingId, [FromBody] ReviewListingRequest request, CancellationToken cancellationToken)
    {
        await birdListings.RejectListingAsync(listingId, request?.Notes, cancellationToken);
        return NoContent();
    }
}
