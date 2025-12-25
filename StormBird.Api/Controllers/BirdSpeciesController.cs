using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StormBird.Infrastructure.Persistence;

namespace StormBird.Api.Controllers;

[ApiController]
[Route("api/species")]
public sealed class BirdSpeciesController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var species = await dbContext.BirdSpeciesSet
            .AsNoTracking()
            .OrderBy(x => x.CommonName)
            .ToListAsync(cancellationToken);

        return Ok(species);
    }
}
