using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? VatNumber { get; set; }
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? PostalAddress { get; set; }
    public string? ShippingAddress { get; set; }
    public string? MarketplaceDisplayName { get; set; }
    public string? MarketplaceTagline { get; set; }
    public string? LogoUrl { get; set; }
    public string? BrandPrimaryColor { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TenantDomain> Domains { get; set; } = new List<TenantDomain>();
}
