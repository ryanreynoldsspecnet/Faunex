using System.ComponentModel.DataAnnotations;

namespace Faunex.Web.Platform;

public sealed record CreatePlatformUserRequest(
    string Email,
    string Password,
    string? DisplayName,
    Guid? TenantId,
    IReadOnlyList<string> Roles);

public sealed record UpdatePlatformUserRequest(
    string Email,
    string? DisplayName,
    Guid? TenantId,
    IReadOnlyList<string> Roles,
    bool IsActive);

public sealed record PlatformUserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    Guid? TenantId,
    IReadOnlyList<string> Roles,
    bool IsActive);

public sealed class PlatformUserFormModel
{
    [Required]
    [EmailAddress]
    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [StringLength(160)]
    public string? DisplayName { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = PlatformRoles.PlatformAdmin;

    public Guid? TenantId { get; set; }

    public bool IsActive { get; set; } = true;
}

public static class PlatformRoles
{
    public const string PlatformSuperAdmin = "PlatformSuperAdmin";
    public const string PlatformAdmin = "PlatformAdmin";
    public const string PlatformComplianceAdmin = "PlatformComplianceAdmin";
    public const string PlatformSupport = "PlatformSupport";
    public const string TenantAdmin = "TenantAdmin";
    public const string Seller = "Seller";
    public const string Buyer = "Buyer";

    public static IReadOnlyList<string> Assignable { get; } =
    [
        PlatformAdmin,
        PlatformComplianceAdmin,
        PlatformSupport,
        PlatformSuperAdmin
    ];

    public static IReadOnlyList<string> AllAssignable { get; } =
    [
        PlatformAdmin,
        PlatformComplianceAdmin,
        PlatformSupport,
        PlatformSuperAdmin,
        TenantAdmin,
        Seller,
        Buyer
    ];

    public static bool IsPlatformRole(string role) =>
        string.Equals(role, PlatformAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, PlatformComplianceAdmin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, PlatformSupport, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, PlatformSuperAdmin, StringComparison.OrdinalIgnoreCase);
}
