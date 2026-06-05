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


public sealed record TenantBrandingDto(
    Guid TenantId,
    string TenantName,
    string? CompanyName,
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? SupportEmail,
    string? SupportPhone,
    string? ContactEmail,
    string? ContactPhone,
    string? PrimaryDomain,
    bool IsActive);

public sealed record UpdateTenantBrandingRequest(
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? SupportEmail,
    string? SupportPhone);

public sealed class TenantBrandingFormModel
{
    [StringLength(160)]
    public string? MarketplaceDisplayName { get; set; }

    [StringLength(240)]
    public string? MarketplaceTagline { get; set; }

    [Url]
    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [RegularExpression("^#(?:[0-9a-fA-F]{3}){1,2}$", ErrorMessage = "Use a hex colour like #1f6f4a.")]
    [StringLength(7)]
    public string? BrandPrimaryColor { get; set; } = "#1f6f4a";

    [EmailAddress]
    [StringLength(160)]
    public string? SupportEmail { get; set; }

    [StringLength(80)]
    public string? SupportPhone { get; set; }

    public static TenantBrandingFormModel FromDto(TenantBrandingDto dto) =>
        new()
        {
            MarketplaceDisplayName = dto.MarketplaceDisplayName,
            MarketplaceTagline = dto.MarketplaceTagline,
            LogoUrl = dto.LogoUrl,
            BrandPrimaryColor = dto.BrandPrimaryColor ?? "#1f6f4a",
            SupportEmail = dto.SupportEmail,
            SupportPhone = dto.SupportPhone
        };
}
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
