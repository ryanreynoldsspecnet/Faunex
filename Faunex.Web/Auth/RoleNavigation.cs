using System.Security.Claims;

namespace Faunex.Web.Auth;

public static class RoleNavigation
{
    private static readonly string[] PlatformRoles =
    [
        "PlatformAdmin",
        "PlatformSuperAdmin",
        "PlatformComplianceAdmin",
        "PlatformSupport"
    ];

    public static bool IsPlatformUser(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return PlatformRoles.Any(role => user.IsInRole(role))
            || user.FindAll("role").Any(c => PlatformRoles.Contains(c.Value, StringComparer.OrdinalIgnoreCase))
            || user.FindAll(ClaimTypes.Role).Any(c => PlatformRoles.Contains(c.Value, StringComparer.OrdinalIgnoreCase))
            || user.HasClaim(c =>
                string.Equals(c.Type, "IsPlatformAdmin", StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase))
            || user.HasClaim(c =>
                string.Equals(c.Type, "is_platform_admin", StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsPlatformUser(bool isPlatformAdmin, IReadOnlyCollection<string>? roles) =>
        isPlatformAdmin || (roles?.Any(role => PlatformRoles.Contains(role, StringComparer.OrdinalIgnoreCase)) ?? false);

    public static bool IsTenantAdmin(IReadOnlyCollection<string>? roles) =>
        roles?.Any(role => string.Equals(role, "TenantAdmin", StringComparison.OrdinalIgnoreCase)) ?? false;
}
