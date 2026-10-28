using Faunex.Api.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/tenancy")]
public sealed class TenantTlsController(TenantDomainResolver domains) : ControllerBase
{
    [HttpGet("tls-authorization")]
    [InfrastructureEndpoint]
    public async Task<IActionResult> AuthorizeTls([FromQuery] string? domain, CancellationToken cancellationToken) =>
        await domains.ResolveTenantIdAsync(domain, cancellationToken) is not null ? Ok() : StatusCode(403);
}
