using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("ping")]
    public ActionResult Ping() => Content("Faunex API is alive", "text/plain");
}
