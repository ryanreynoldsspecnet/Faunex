using System.Security.Claims;

namespace Faunex.Application.Auth;

public static class IdentityTenantMapping
{
    public static Guid? ResolveTenantId(ClaimsPrincipal? principal)
    {
        // Contract:
        // - Platform admins MUST NOT have a tenant id claim.
        // - Tenant users MUST have tenant_id.
        // TODO: Replace with provider-specific mapping if claim names differ.

        var tenantIdRaw = principal?.FindFirst(FaunexClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantIdRaw, out var tenantId) ? tenantId : null;
    }

    public static bool ResolveIsPlatformAdmin(ClaimsPrincipal? principal)
    {
        // Contract:
        // - is_platform_admin=true indicates a platform admin identity.
        // - Platform admins bypass tenant scoping.
        // TODO: In the real auth integration, validate that platform admin identities
        // also have an allowed platform role.

        var raw = principal?.FindFirst(FaunexClaimTypes.IsPlatformAdmin)?.Value;
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyCollection<string> ResolveRoles(ClaimsPrincipal? principal)
    {
        // Contract:
        // - One or more roles emitted as standard ClaimTypes.Role.
        // NOTE: Some identity providers emit "role" or "roles"; we standardize on ClaimTypes.Role.

        if (principal is null)
        {
            return Array.Empty<string>();
        }

        return principal.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .ToArray();
    }
}
