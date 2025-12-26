using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/listings")]
public sealed class ListingsQueryController(IListingQueryService listings) : ControllerBase
{
    [HttpGet("browse")]
    public Task<PagedResult<ListingDto>> Browse(
        [FromQuery] string? animalClass,
        [FromQuery] Guid? speciesId,
        [FromQuery] string? location,
        [FromQuery] bool? activeOnly,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListingQuery(animalClass, speciesId, location, activeOnly, skip, take);
        return listings.BrowseAsync(query, cancellationToken);
    }

    [HttpGet("my")]
    public Task<PagedResult<ListingDto>> My(
        [FromQuery] Guid sellerId,
        [FromQuery] string? animalClass,
        [FromQuery] Guid? speciesId,
        [FromQuery] string? location,
        [FromQuery] bool? activeOnly,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListingQuery(animalClass, speciesId, location, activeOnly, skip, take);
        return listings.GetMyListingsAsync(sellerId, query, cancellationToken);
    }

    [HttpGet("tenant")]
    public Task<PagedResult<ListingDto>> Tenant(
        [FromQuery] string? animalClass,
        [FromQuery] Guid? speciesId,
        [FromQuery] string? location,
        [FromQuery] bool? activeOnly,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListingQuery(animalClass, speciesId, location, activeOnly, skip, take);
        return listings.GetTenantListingsAsync(query, cancellationToken);
    }

    [HttpGet("all")]
    public Task<PagedResult<ListingDto>> All(
        [FromQuery] string? animalClass,
        [FromQuery] Guid? speciesId,
        [FromQuery] string? location,
        [FromQuery] bool? activeOnly,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListingQuery(animalClass, speciesId, location, activeOnly, skip, take);
        return listings.GetAllListingsAsync(query, cancellationToken);
    }
}
