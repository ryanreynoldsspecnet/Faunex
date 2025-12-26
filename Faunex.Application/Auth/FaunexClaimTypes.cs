namespace Faunex.Application.Auth;

public static class FaunexClaimTypes
{
    // Claims contract (authoritative):
    // - tenant_id: Guid of the tenant (required for tenant users; absent for platform admins)
    // - role: one or more role names (see FaunexRoles)
    // - is_platform_admin: "true"/"false" (platform admins bypass tenant scoping)

    public const string TenantId = "tenant_id";
    public const string Role = "role";
    public const string IsPlatformAdmin = "is_platform_admin";
}
