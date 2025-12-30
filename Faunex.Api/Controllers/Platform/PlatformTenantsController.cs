using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers.Platform;

[ApiController]
[Route("api/platform")]
[Authorize(Policy = "PlatformAdminOnly")]
public sealed class PlatformTenantsController : ControllerBase
{
    [HttpPost("tenants")]
    public ActionResult<TenantDto> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    [HttpGet("tenants")]
    public ActionResult<IReadOnlyList<TenantDto>> GetTenants(CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

public sealed record CreateTenantRequest(
    string Name,
    string? Slug = null,
    bool IsActive = true);

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? Slug,
    bool IsActive,
    DateTimeOffset CreatedUtc);
