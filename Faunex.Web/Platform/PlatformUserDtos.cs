using System.ComponentModel.DataAnnotations;

namespace Faunex.Web.Platform;

public sealed record CreatePlatformUserRequest(
    string Email,
    string Password,
    string? DisplayName,
    Guid? TenantId,
    IReadOnlyList<string> Roles);

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
}

public static class PlatformRoles
{
    public const string PlatformSuperAdmin = "PlatformSuperAdmin";
    public const string PlatformAdmin = "PlatformAdmin";
    public const string PlatformComplianceAdmin = "PlatformComplianceAdmin";
    public const string PlatformSupport = "PlatformSupport";

    public static IReadOnlyList<string> Assignable { get; } =
    [
        PlatformAdmin,
        PlatformComplianceAdmin,
        PlatformSupport,
        PlatformSuperAdmin
    ];
}
