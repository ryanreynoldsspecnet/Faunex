using System.Security.Claims;
using Faunex.Application.Auth;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Auth;

public sealed class AccountAccessService(UserManager<ApplicationUser> users, ApplicationDbContext db)
{
    public async Task<bool> CanSignInAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            return false;
        }

        var roles = await users.GetRolesAsync(user);
        if (!UserAccessRules.ValidateRoleTenantShape(user.TenantId, roles.ToArray()).IsValid
            || user.IsPlatformAdmin != UserAccessRules.IsPlatformRoleSet(roles))
        {
            return false;
        }

        return !user.TenantId.HasValue || await db.Tenants.AsNoTracking()
            .AnyAsync(x => x.Id == user.TenantId.Value && x.IsActive, cancellationToken);
    }

    public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password, CancellationToken cancellationToken = default)
    {
        if (!await CanSignInAsync(user, cancellationToken))
        {
            return false;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            if (user.LockoutEnabled)
            {
                await users.AccessFailedAsync(user);
            }

            return false;
        }

        return (await users.ResetAccessFailedCountAsync(user)).Succeeded;
    }

    public async Task<bool> ValidateSessionAsync(ClaimsPrincipal? principal, CancellationToken cancellationToken = default)
    {
        if (principal?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
        {
            return false;
        }

        var user = await users.FindByIdAsync(actorId.ToString());
        var stamp = principal.FindFirstValue(FaunexClaimTypes.SecurityStamp);
        if (user is null || string.IsNullOrEmpty(stamp)
            || !string.Equals(stamp, user.SecurityStamp, StringComparison.Ordinal)
            || !await CanSignInAsync(user, cancellationToken))
        {
            return false;
        }

        var tenantClaim = principal.FindFirstValue(FaunexClaimTypes.TenantId);
        if (user.TenantId.HasValue
            ? !Guid.TryParse(tenantClaim, out var tenantId) || tenantId != user.TenantId.Value
            : tenantClaim is not null)
        {
            return false;
        }

        var expectedPlatformFlag = user.IsPlatformAdmin ? "true" : "false";
        if (!string.Equals(principal.FindFirstValue(FaunexClaimTypes.IsPlatformAdmin), expectedPlatformFlag, StringComparison.Ordinal)
            || !string.Equals(principal.FindFirstValue(ClaimTypes.Email), user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var currentRoles = await users.GetRolesAsync(user);
        return currentRoles.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(principal.FindAll(ClaimTypes.Role).Select(x => x.Value));
    }
}
