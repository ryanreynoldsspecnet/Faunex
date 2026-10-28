using System.Security.Claims;
using Faunex.Application.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Auth;

public sealed class UserMutationGuard(UserManager<ApplicationUser> users, ApplicationIdentityDbContext identityDb)
{
    public async Task<ActionResult?> ValidateAsync(ClaimsPrincipal actor, ApplicationUser target,
        IReadOnlyCollection<string> roles, Guid? tenantId, bool active, bool deleting = false)
    {
        var oldRoles = await users.GetRolesAsync(target);
        if (!actor.IsInRole(FaunexRoles.PlatformSuperAdmin)
            && (oldRoles.Contains(FaunexRoles.PlatformSuperAdmin) || roles.Contains(FaunexRoles.PlatformSuperAdmin)))
        {
            return new ForbidResult();
        }

        if (actor.FindFirstValue(ClaimTypes.NameIdentifier) == target.Id.ToString() && (deleting || !active))
        {
            return new BadRequestObjectResult(new { error = "You cannot delete or disable your own account while logged in." });
        }

        if (target.LockoutEnd > DateTimeOffset.UtcNow)
        {
            return null;
        }

        if (oldRoles.Contains(FaunexRoles.PlatformSuperAdmin)
            && (deleting || !active || !roles.Contains(FaunexRoles.PlatformSuperAdmin))
            && !await HasOtherAdminAsync(FaunexRoles.PlatformSuperAdmin, target, null))
        {
            return new ConflictObjectResult(new { error = "At least one active platform super administrator is required." });
        }

        if (target.TenantId.HasValue && oldRoles.Contains(FaunexRoles.TenantAdmin)
            && (deleting || !active || tenantId != target.TenantId || !roles.Contains(FaunexRoles.TenantAdmin))
            && !await HasOtherAdminAsync(FaunexRoles.TenantAdmin, target, target.TenantId))
        {
            return new ConflictObjectResult(new { error = "At least one active tenant administrator is required." });
        }

        return null;
    }

    private async Task<bool> HasOtherAdminAsync(string role, ApplicationUser target, Guid? tenantId)
    {
        var normalizedRole = role.ToUpperInvariant();
        var admins = await identityDb.Users.AsNoTracking()
            .Where(x => x.Id != target.Id && x.TenantId == tenantId
                && identityDb.UserRoles.Any(link => link.UserId == x.Id
                    && identityDb.Roles.Any(r => r.Id == link.RoleId && r.NormalizedName == normalizedRole)))
            .ToListAsync();
        return admins.Any(x => x.LockoutEnd is null || x.LockoutEnd <= DateTimeOffset.UtcNow);
    }
}
