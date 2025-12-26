namespace Faunex.Application.Auth;

public static class FaunexRoles
{
    // Platform roles (not tenant-scoped)
    public const string PlatformSuperAdmin = "PlatformSuperAdmin";
    public const string PlatformComplianceAdmin = "PlatformComplianceAdmin";
    public const string PlatformSupport = "PlatformSupport";

    // Tenant roles (tenant-scoped)
    public const string TenantAdmin = "TenantAdmin";
    public const string Seller = "Seller";
    public const string Buyer = "Buyer";
    public const string Staff = "Staff";

    public static IReadOnlyCollection<string> All { get; } =
    [
        PlatformSuperAdmin,
        PlatformComplianceAdmin,
        PlatformSupport,
        TenantAdmin,
        Seller,
        Buyer,
        Staff
    ];
}
