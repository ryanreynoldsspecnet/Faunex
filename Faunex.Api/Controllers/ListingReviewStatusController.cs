using Faunex.Api.Compliance;
using Faunex.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers;

[ApiController]
[Authorize(Policy = "ListingManager")]
[Route("api/listings/{listingId:guid}/review")]
public sealed class ListingReviewStatusController(ListingReviewQueries queries) : ControllerBase
{
    [HttpGet]
    public Task<ListingReviewDto> Detail(Guid listingId, CancellationToken ct) => queries.DetailAsync(listingId, ct);
}
