using System.ComponentModel.DataAnnotations;

namespace Faunex.Web.Platform;

public sealed class PlatformTenantEditFormModel
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

    public bool IsActive { get; set; } = true;

    public static PlatformTenantEditFormModel FromTenant(TenantDto tenant) =>
        new()
        {
            Name = tenant.Name,
            CompanyName = tenant.CompanyName,
            RegistrationNumber = tenant.RegistrationNumber,
            VatNumber = tenant.VatNumber,
            ContactFirstName = tenant.ContactFirstName,
            ContactLastName = tenant.ContactLastName,
            ContactEmail = tenant.ContactEmail,
            ContactPhone = tenant.ContactPhone,
            PhysicalAddress = tenant.PhysicalAddress,
            PostalAddress = tenant.PostalAddress,
            ShippingAddress = tenant.ShippingAddress,
            MarketplaceDisplayName = tenant.MarketplaceDisplayName,
            MarketplaceTagline = tenant.MarketplaceTagline,
            LogoUrl = tenant.LogoUrl,
            BrandPrimaryColor = tenant.BrandPrimaryColor ?? "#1f6f4a",
            SupportEmail = tenant.SupportEmail,
            SupportPhone = tenant.SupportPhone,
            IsActive = tenant.IsActive
        };
}
