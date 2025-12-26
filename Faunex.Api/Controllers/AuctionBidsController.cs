using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/auctions/{auctionId:guid}")]
public sealed class AuctionBidsController(IBidService bids, IAuctionService auctions) : ControllerBase
{
    [HttpPost("bids")]
    public async Task<ActionResult<BidDto>> PlaceBid(Guid auctionId, [FromBody] PlaceBidRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var bid = await bids.PlaceBidAsync(auctionId, request.Amount, cancellationToken);
            return Ok(bid);
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

    [HttpGet("bids")]
    public async Task<ActionResult<PagedResult<BidDto>>> GetBids(Guid auctionId, [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await bids.GetBidsForAuctionAsync(auctionId, skip, take, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Not found is signaled as InvalidOperationException("...") currently.
            // Keep it simple for now.
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("price")]
    public async Task<ActionResult<CurrentPriceDto>> GetPrice(Guid auctionId, CancellationToken cancellationToken)
    {
        try
        {
            var price = await bids.GetCurrentPriceAsync(auctionId, cancellationToken);
            return Ok(new CurrentPriceDto(price));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open(Guid auctionId, CancellationToken cancellationToken)
    {
        try
        {
            await auctions.OpenAuctionAsync(auctionId, cancellationToken);
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

    [HttpPost("close")]
    public async Task<IActionResult> Close(Guid auctionId, CancellationToken cancellationToken)
    {
        try
        {
            await auctions.CloseAsync(auctionId, cancellationToken);
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
