using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/seller/listings")]
public sealed class SellerListingsController(IBirdListingService birdListings) : ControllerBase
{
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
