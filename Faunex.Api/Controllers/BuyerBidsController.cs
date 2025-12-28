using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/buyer/bids")]
[Authorize(Policy = "BuyerOnly")]
public sealed class BuyerBidsController(IBidService bids) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> PlaceBid([FromBody] CreateBidRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await bids.PlaceBidAsync(request, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
