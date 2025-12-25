using Microsoft.AspNetCore.Mvc;

namespace StormBird.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("ping")]
    public ActionResult Ping() => Content("StormBird API is alive", "text/plain");
}
