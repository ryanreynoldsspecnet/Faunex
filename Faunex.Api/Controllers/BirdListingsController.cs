using Microsoft.AspNetCore.Mvc;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BirdListingsController(IBirdListingService listings) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BirdListingDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var listing = await listings.GetByIdAsync(id, cancellationToken);
        return listing is null ? NotFound() : Ok(listing);
    }

    [HttpGet("by-seller/{sellerId:guid}")]
    public async Task<ActionResult<IReadOnlyList<BirdListingDto>>> GetBySellerId(Guid sellerId, CancellationToken cancellationToken)
    {
        var results = await listings.GetBySellerIdAsync(sellerId, cancellationToken);
        return Ok(results);
    }

    [Authorize(Policy = "SellerOnly")]
    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] BirdListingDto listing, CancellationToken cancellationToken)
    {
        var id = await listings.CreateAsync(listing, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [Authorize(Policy = "SellerOnly")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] BirdListingDto listing, CancellationToken cancellationToken)
    {
        var updated = listing with { Id = id };
        await listings.UpdateAsync(updated, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "SellerOnly")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await listings.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
