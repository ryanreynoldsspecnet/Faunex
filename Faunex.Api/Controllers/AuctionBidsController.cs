using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/auctions/{auctionId:guid}")]
public sealed class AuctionBidsController(IBidService bids, IAuctionService auctions) : ControllerBase
{
    [Authorize(Policy = "BuyerOnly")]
    [HttpPost("bids")]
    public async Task<ActionResult<BidDto>> PlaceBid(Guid auctionId, [FromBody] PlaceBidRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Response.Headers.TryAdd("Deprecation", "true");
            Response.Headers.TryAdd("Link", "</api/buyer/bids>; rel=\"alternate\"");

            var bid = await bids.PlaceBidAsync(auctionId, request.Amount, cancellationToken);
            return Ok(bid);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex) when (ex is not ListingEditConflictException)
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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex) when (ex is not ListingEditConflictException)
        {
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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [Authorize(Policy = "TenantAdminOnly")]
    [HttpPost("open")]
    public async Task<IActionResult> Open(Guid auctionId, [FromBody] LifecycleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await auctions.OpenAuctionAsync(auctionId, cancellationToken, request.Revision);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex) when (ex is not ListingEditConflictException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = "TenantAdminOnly")]
    [HttpPost("close")]
    public async Task<IActionResult> Close(Guid auctionId, [FromBody] LifecycleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await auctions.CloseAsync(auctionId, cancellationToken, request.Revision);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex) when (ex is not ListingEditConflictException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public sealed record LifecycleRequest(Guid? Revision);
