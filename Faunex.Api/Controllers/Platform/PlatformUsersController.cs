using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers.Platform;

[ApiController]
[Route("api/platform")]
[Authorize(Policy = "PlatformAdminOnly")]
public sealed class PlatformUsersController : ControllerBase
{
    [HttpPost("users")]
    public ActionResult<PlatformUserDto> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    [HttpGet("users")]
    public ActionResult<IReadOnlyList<PlatformUserDto>> GetUsers([FromQuery] Guid? tenantId = null, [FromQuery] string? role = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    [HttpPut("users/{userId:guid}/tenant")]
    public IActionResult SetUserTenant(Guid userId, [FromBody] SetUserTenantRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    [HttpPut("users/{userId:guid}/roles")]
    public IActionResult SetUserRoles(Guid userId, [FromBody] SetUserRolesRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

public sealed record CreateUserRequest(
    string Email,
    string Password,
    string? DisplayName = null,
    Guid? TenantId = null,
    IReadOnlyList<string>? Roles = null);

public sealed record PlatformUserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    Guid? TenantId,
    IReadOnlyList<string> Roles,
    bool IsActive);

public sealed record SetUserTenantRequest(Guid? TenantId);

public sealed record SetUserRolesRequest(IReadOnlyList<string> Roles);
