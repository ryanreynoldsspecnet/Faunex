namespace Faunex.Application.Auth;

public static class FaunexRoles
{
    // Minimal baseline roles
    public const string PlatformAdmin = "PlatformAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Seller = "Seller";
    public const string Buyer = "Buyer";

    // Existing platform roles (kept for compatibility with existing policies/pages)
    public const string PlatformSuperAdmin = "PlatformSuperAdmin";
    public const string PlatformComplianceAdmin = "PlatformComplianceAdmin";
    public const string PlatformSupport = "PlatformSupport";

    // Other existing roles (kept; not part of the baseline scope)
    public const string Staff = "Staff";

    public static IReadOnlyCollection<string> All { get; } =
    [
        PlatformAdmin,
        TenantAdmin,
        Seller,
        Buyer,
        PlatformSuperAdmin,
        PlatformComplianceAdmin,
        PlatformSupport,
        Staff
    ];
}
