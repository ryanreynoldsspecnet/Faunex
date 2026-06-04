using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers.Platform;

[ApiController]
[Route("api/platform")]
[Authorize(Policy = "PlatformAdminOnly")]
public sealed class PlatformUsersController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles) : ControllerBase
{
    [HttpPost("users")]
    public async Task<ActionResult<PlatformUserDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required." });
        }

        var assignedRoles = UserAccessRules.NormalizeRoles(request.Roles);
        var validation = await ValidateAssignmentAsync(request.TenantId, assignedRoles, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            return Conflict(new { error = "A user with this email already exists." });
        }

        var isPlatformAdmin = UserAccessRules.IsPlatformRoleSet(assignedRoles);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            TenantId = isPlatformAdmin ? null : request.TenantId,
            IsPlatformAdmin = isPlatformAdmin
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return BadRequest(new { errors = created.Errors.Select(e => e.Description).ToArray() });
        }

        var roleResult = await SetRolesInternalAsync(user, assignedRoles);
        if (!roleResult.Succeeded)
        {
            return BadRequest(new { errors = roleResult.Errors.Select(e => e.Description).ToArray() });
        }

        return CreatedAtAction(nameof(GetUsers), new { userId = user.Id }, await ToDtoAsync(user));
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<PlatformUserDto>>> GetUsers([FromQuery] Guid? tenantId = null, [FromQuery] string? role = null, CancellationToken cancellationToken = default)
    {
        var query = users.Users.AsNoTracking();

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var result = new List<PlatformUserDto>();
        foreach (var user in await query.OrderBy(x => x.Email).ToListAsync(cancellationToken))
        {
            var dto = await ToDtoAsync(user);
            if (!string.IsNullOrWhiteSpace(role) && !dto.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(dto);
        }

        return Ok(result);
    }

    [HttpPut("users/{userId:guid}/tenant")]
    public async Task<IActionResult> SetUserTenant(Guid userId, [FromBody] SetUserTenantRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var currentRoles = (await users.GetRolesAsync(user)).ToArray();
        var validation = await ValidateAssignmentAsync(request.TenantId, currentRoles, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        user.TenantId = request.TenantId;
        user.IsPlatformAdmin = UserAccessRules.IsPlatformRoleSet(currentRoles);

        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return BadRequest(new { errors = updated.Errors.Select(e => e.Description).ToArray() });
        }

        return NoContent();
    }

    [HttpPut("users/{userId:guid}/roles")]
    public async Task<IActionResult> SetUserRoles(Guid userId, [FromBody] SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        var assignedRoles = UserAccessRules.NormalizeRoles(request.Roles);
        var validation = await ValidateAssignmentAsync(user.TenantId, assignedRoles, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var roleResult = await SetRolesInternalAsync(user, assignedRoles);
        if (!roleResult.Succeeded)
        {
            return BadRequest(new { errors = roleResult.Errors.Select(e => e.Description).ToArray() });
        }

        user.IsPlatformAdmin = UserAccessRules.IsPlatformRoleSet(assignedRoles);
        if (user.IsPlatformAdmin)
        {
            user.TenantId = null;
        }

        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return BadRequest(new { errors = updated.Errors.Select(e => e.Description).ToArray() });
        }

        return NoContent();
    }

    private async Task<ActionResult?> ValidateAssignmentAsync(Guid? tenantId, IReadOnlyCollection<string> assignedRoles, CancellationToken cancellationToken)
    {
        var shape = UserAccessRules.ValidateRoleTenantShape(tenantId, assignedRoles);

        if (!shape.IsValid)
        {
            return BadRequest(new { error = shape.Error });
        }

        foreach (var role in assignedRoles)
        {
            if (await roles.RoleExistsAsync(role))
            {
                continue;
            }

            var created = await roles.CreateAsync(new IdentityRole<Guid>(role));
            if (!created.Succeeded)
            {
                return BadRequest(new { errors = created.Errors.Select(e => e.Description).ToArray() });
            }
        }

        if (tenantId.HasValue)
        {
            var tenantExists = await db.Tenants.AnyAsync(x => x.Id == tenantId.Value && x.IsActive, cancellationToken);
            if (!tenantExists)
            {
                return BadRequest(new { error = "Tenant does not exist or is inactive." });
            }
        }

        return null;
    }

    private async Task<IdentityResult> SetRolesInternalAsync(ApplicationUser user, IReadOnlyCollection<string> assignedRoles)
    {
        var currentRoles = await users.GetRolesAsync(user);
        var remove = currentRoles.Except(assignedRoles, StringComparer.OrdinalIgnoreCase).ToArray();
        var add = assignedRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToArray();

        if (remove.Length > 0)
        {
            var removed = await users.RemoveFromRolesAsync(user, remove);
            if (!removed.Succeeded)
            {
                return removed;
            }
        }

        if (add.Length > 0)
        {
            return await users.AddToRolesAsync(user, add);
        }

        return IdentityResult.Success;
    }

    private async Task<PlatformUserDto> ToDtoAsync(ApplicationUser user)
    {
        var assignedRoles = await users.GetRolesAsync(user);
        return new PlatformUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.TenantId,
            assignedRoles.ToArray(),
            IsActive: user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow);
    }

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
