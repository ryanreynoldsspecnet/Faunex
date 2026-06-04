using System.ComponentModel.DataAnnotations;

namespace Faunex.Web.TenantAdmin;

public sealed record TenantDashboardDto(
    Guid TenantId,
    string TenantName,
    string? CompanyName,
    string? PrimaryDomain,
    bool IsActive,
    int UserCount,
    int ListingCount,
    int ComplianceQueueCount);

public sealed record TenantUserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    bool IsActive);

public sealed record CreateTenantUserRequest(
    string Email,
    string Password,
    string? DisplayName,
    IReadOnlyList<string> Roles);

public sealed record UpdateTenantUserRequest(
    string Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    bool IsActive);

public sealed class TenantUserFormModel
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
    public string Role { get; set; } = TenantRoles.Seller;

    public bool IsActive { get; set; } = true;
}

public static class TenantRoles
{
    public const string TenantAdmin = "TenantAdmin";
    public const string Seller = "Seller";
    public const string Buyer = "Buyer";

    public static IReadOnlyList<string> Assignable { get; } =
    [
        Seller,
        Buyer,
        TenantAdmin
    ];
}
