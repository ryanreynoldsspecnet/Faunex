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
            IsActive = tenant.IsActive
        };
}
