using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Faunex.Shared;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(HealthCheckService health, IConfiguration configuration) : ControllerBase
{
    [HttpGet("ping")]
    [Faunex.Api.Tenancy.InfrastructureEndpoint]
    public ActionResult Ping() => Content("Faunex API is alive", "text/plain");

    [HttpGet("ready")]
    [Faunex.Api.Tenancy.InfrastructureEndpoint]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var report = await health.CheckHealthAsync(cancellationToken);
        return new ContentResult { StatusCode = report.Status == HealthStatus.Healthy ? 200 : 503,
            ContentType = "text/plain", Content = report.Status == HealthStatus.Healthy
                ? ReleaseIdentity.Ready(configuration["ReleaseRevision"]) : "not ready" };
    }
}
