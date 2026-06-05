using System.ComponentModel.DataAnnotations;

namespace Faunex.Web.Platform;

public sealed class PlatformTenantFormModel
{
    [Required]
    [StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(160)]
    public string? CompanyName { get; set; }

    [StringLength(80)]
    public string? RegistrationNumber { get; set; }

    [StringLength(80)]
    public string? VatNumber { get; set; }

    [StringLength(80)]
    public string? ContactFirstName { get; set; }

    [StringLength(80)]
    public string? ContactLastName { get; set; }

    [EmailAddress]
    [StringLength(160)]
    public string? ContactEmail { get; set; }

    [StringLength(80)]
    public string? ContactPhone { get; set; }

    [StringLength(500)]
    public string? PhysicalAddress { get; set; }

    [StringLength(500)]
    public string? PostalAddress { get; set; }

    [StringLength(500)]
    public string? ShippingAddress { get; set; }

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

    [StringLength(160)]
    public string? PrimaryDomain { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    [EmailAddress]
    [StringLength(160)]
    public string TenantAdminEmail { get; set; } = string.Empty;

    [StringLength(160)]
    public string? TenantAdminDisplayName { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string TenantAdminPassword { get; set; } = string.Empty;
}
