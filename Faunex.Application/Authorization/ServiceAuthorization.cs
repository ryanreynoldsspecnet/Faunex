using Faunex.Application.Auth;
using Faunex.Application.Interfaces;

namespace Faunex.Application.Authorization;

public static class ServiceAuthorization
{
    public static void EnsureTenantUser(ITenantContext tenantContext)
    {
        if (tenantContext.IsPlatformAdmin)
        {
            return;
        }

        if (!tenantContext.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException("Tenant user context is required.");
        }
    }

    public static void EnsureNotPlatformAdminForWrite(ITenantContext tenantContext, string action)
    {
        if (tenantContext.IsPlatformAdmin)
        {
            // Platform admins can read everything, but writes must be explicitly allowed.
            throw new UnauthorizedAccessException($"Platform admins are not allowed to {action}.");
        }

        if (!tenantContext.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException("TenantId is required.");
        }
    }

    public static void EnsurePlatformAdmin(ITenantContext tenantContext)
    {
        if (!tenantContext.IsPlatformAdmin)
        {
            throw new UnauthorizedAccessException("Platform admin context is required.");
        }
    }

    public static void EnsureRole(ITenantContext tenantContext, params string[] allowedRoles)
    {
        var roles = GetRoles(tenantContext);

        if (roles.Count == 0)
        {
            throw new UnauthorizedAccessException("User role is required.");
        }

        if (!allowedRoles.Any(roles.Contains))
        {
            throw new UnauthorizedAccessException("User is not authorized for this action.");
        }
    }

    public static void EnsureTenantAdminOrSeller(ITenantContext tenantContext) =>
        EnsureRole(tenantContext, FaunexRoles.TenantAdmin, FaunexRoles.Seller);

    public static void EnsureTenantAdmin(ITenantContext tenantContext) =>
        EnsureRole(tenantContext, FaunexRoles.TenantAdmin);

    public static void EnsurePlatformComplianceAdmin(ITenantContext tenantContext) =>
        EnsureRole(tenantContext, FaunexRoles.PlatformComplianceAdmin, FaunexRoles.PlatformSuperAdmin);

    public static void EnsureSellerOrTenantAdminOwnsSellerId(ITenantContext tenantContext, Guid sellerId)
    {
        EnsureRole(tenantContext, FaunexRoles.TenantAdmin, FaunexRoles.Seller);

        var roles = GetRoles(tenantContext);

        if (roles.Contains(FaunexRoles.TenantAdmin))
        {
            return;
        }

        // Seller ownership rule (current baseline): seller can only act on their own seller id.
        // TODO: Replace/augment with identity-user-id once User identity is wired.
        if (tenantContext is ITenantContextWithActor actor && actor.ActorId.HasValue)
        {
            if (actor.ActorId.Value != sellerId)
            {
                throw new UnauthorizedAccessException("Seller can only act on their own listings.");
            }

            return;
        }

        throw new UnauthorizedAccessException("Seller identity is required to enforce ownership.");
    }

    private static IReadOnlyCollection<string> GetRoles(ITenantContext tenantContext)
    {
        if (tenantContext is ITenantContextWithRoles withRoles)
        {
            return withRoles.Roles;
        }

        // TODO: Once a real auth-backed ITenantContext exists, this should always be available.
        return Array.Empty<string>();
    }
}

public interface ITenantContextWithRoles
{
    IReadOnlyCollection<string> Roles { get; }
}

public interface ITenantContextWithActor
{
    Guid? ActorId { get; }
}
