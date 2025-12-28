using Microsoft.AspNetCore.Mvc;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuctionsController(IAuctionService auctions) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuctionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var auction = await auctions.GetByIdAsync(id, cancellationToken);
        return auction is null ? NotFound() : Ok(auction);
    }

    [HttpGet("by-listing/{listingId:guid}")]
    public async Task<ActionResult<IReadOnlyList<AuctionDto>>> GetByListingId(Guid listingId, CancellationToken cancellationToken)
    {
        var results = await auctions.GetByListingIdAsync(listingId, cancellationToken);
        return Ok(results);
    }

    [Authorize(Policy = "SellerOnly")]
    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] AuctionDto auction, CancellationToken cancellationToken)
    {
        var id = await auctions.CreateAsync(auction, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [Authorize(Policy = "SellerOnly")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AuctionDto auction, CancellationToken cancellationToken)
    {
        var updated = auction with { Id = id };
        await auctions.UpdateAsync(updated, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "SellerOnly")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await auctions.CloseAsync(id, cancellationToken);
        return NoContent();
    }
}
