using Microsoft.AspNetCore.Mvc;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SpeciesController(ISpeciesLookupService species) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpeciesDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await species.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<SpeciesDto>>> Search([FromQuery] string query, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var results = await species.SearchAsync(query, take, cancellationToken);
        return Ok(results);
    }

    [HttpGet("by-scientific-name/{scientificName}")]
    public async Task<ActionResult<SpeciesDto>> FindByScientificName(string scientificName, CancellationToken cancellationToken)
    {
        var result = await species.FindByScientificNameAsync(scientificName, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
