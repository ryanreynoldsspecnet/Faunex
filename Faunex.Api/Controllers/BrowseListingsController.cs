using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/browse/listings")]
public sealed class BrowseListingsController(IListingBrowseService browseService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ListingDto>>> Browse(CancellationToken cancellationToken)
    {
        var results = await browseService.BrowseApprovedListingsAsync(cancellationToken);
        return Ok(results);
    }
}
