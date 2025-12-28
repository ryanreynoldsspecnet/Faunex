using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/seller/listings")]
[Authorize(Policy = "SellerOnly")]
public sealed class SellerListingsController(IBirdListingService birdListings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ListingDto>>> GetSellerListings([FromQuery] Guid sellerId, CancellationToken cancellationToken)
    {
        var results = await birdListings.GetSellerListingsAsync(sellerId, cancellationToken);
        return Ok(results);
    }

    [HttpPost("{listingId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid listingId, CancellationToken cancellationToken)
    {
        await birdListings.SubmitForComplianceAsync(listingId, cancellationToken);
        return NoContent();
    }

    [HttpPost("bird")]
    public async Task<ActionResult<ListingDto>> CreateBirdListing([FromBody] CreateBirdListingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var listing = await birdListings.CreateBirdListingAsync(request, cancellationToken);
            return Created(string.Empty, listing);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
