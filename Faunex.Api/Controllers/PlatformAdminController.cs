using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/platform/admin")]
[Authorize(Roles = FaunexRoles.PlatformAdmin + "," + FaunexRoles.PlatformSuperAdmin)]
public sealed class PlatformAdminController(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    ApplicationDbContext appDb) : ControllerBase
{
    public sealed record UserSummary(Guid ActorId, string? Email, Guid? TenantId, bool IsPlatformAdmin, IReadOnlyList<string> Roles);

    public sealed record SetUserRolesRequest(IReadOnlyList<string> Add, IReadOnlyList<string> Remove);

    public sealed record SetUserTenantRequest(Guid? TenantId);

    public sealed record CreateTenantRequest(string Name);

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserSummary>>> GetUsers(CancellationToken cancellationToken)
    {
        var list = await users.Users
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(x => new { x.Id, x.Email, x.TenantId, x.IsPlatformAdmin })
            .ToListAsync(cancellationToken);

        var results = new List<UserSummary>(list.Count);
        foreach (var u in list)
        {
            var user = await users.FindByIdAsync(u.Id.ToString());
            if (user is null)
            {
                continue;
            }

            var userRoles = await users.GetRolesAsync(user);
            results.Add(new UserSummary(u.Id, u.Email, u.TenantId, u.IsPlatformAdmin, userRoles.ToArray()));
        }

        return Ok(results);
    }

    [HttpPost("users/{actorId:guid}/roles")]
    public async Task<IActionResult> SetUserRoles(Guid actorId, [FromBody] SetUserRolesRequest request)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        foreach (var role in request.Add ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }

            if (!await roles.RoleExistsAsync(role))
            {
                return BadRequest(new { error = $"Role '{role}' does not exist." });
            }

            if (!await users.IsInRoleAsync(user, role))
            {
                var add = await users.AddToRoleAsync(user, role);
                if (!add.Succeeded)
                {
                    return BadRequest(new { errors = add.Errors.Select(e => e.Description).ToArray() });
                }
            }
        }

        foreach (var role in request.Remove ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                continue;
            }

            if (await users.IsInRoleAsync(user, role))
            {
                var remove = await users.RemoveFromRoleAsync(user, role);
                if (!remove.Succeeded)
                {
                    return BadRequest(new { errors = remove.Errors.Select(e => e.Description).ToArray() });
                }
            }
        }

        return NoContent();
    }

    [HttpPost("users/{actorId:guid}/tenant")]
    public async Task<IActionResult> SetUserTenant(Guid actorId, [FromBody] SetUserTenantRequest request)
    {
        var user = await users.FindByIdAsync(actorId.ToString());
        if (user is null)
        {
            return NotFound();
        }

        user.TenantId = request.TenantId;

        if (request.TenantId.HasValue)
        {
            user.IsPlatformAdmin = false;
        }

        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToArray() });
        }

        return NoContent();
    }

    [HttpPost("tenants")]
    public async Task<ActionResult<Tenant>> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Tenant name is required." });
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        appDb.Tenants.Add(tenant);
        await appDb.SaveChangesAsync(cancellationToken);

        return Created($"/api/platform/admin/tenants/{tenant.Id}", tenant);
    }
}
