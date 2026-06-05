namespace Faunex.Web.Platform;

public sealed record CreateTenantRequest(
    string Name,
    string? Slug,
    string? CompanyName,
    string? RegistrationNumber,
    string? VatNumber,
    string? ContactFirstName,
    string? ContactLastName,
    string? ContactEmail,
    string? ContactPhone,
    string? PhysicalAddress,
    string? PostalAddress,
    string? ShippingAddress,
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? SupportEmail,
    string? SupportPhone,
    bool IsActive,
    string? FirstAdminEmail,
    string? FirstAdminDisplayName,
    string? FirstAdminPassword);

public sealed record UpdateTenantRequest(
    string Name,
    string? Slug,
    string? CompanyName,
    string? RegistrationNumber,
    string? VatNumber,
    string? ContactFirstName,
    string? ContactLastName,
    string? ContactEmail,
    string? ContactPhone,
    string? PhysicalAddress,
    string? PostalAddress,
    string? ShippingAddress,
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? SupportEmail,
    string? SupportPhone,
    bool IsActive);

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? Slug,
    string? CompanyName,
    string? RegistrationNumber,
    string? VatNumber,
    string? ContactFirstName,
    string? ContactLastName,
    string? ContactEmail,
    string? ContactPhone,
    string? PhysicalAddress,
    string? PostalAddress,
    string? ShippingAddress,
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? SupportEmail,
    string? SupportPhone,
    string? PrimaryDomain,
    int DomainCount,
    bool IsActive,
    DateTimeOffset CreatedUtc);

public sealed record CreateTenantDomainRequest(
    string Hostname,
    bool IsPrimary,
    bool IsActive);

public sealed record TenantDomainDto(
    Guid Id,
    Guid TenantId,
    string Hostname,
    bool IsPrimary,
    bool IsActive,
    DateTimeOffset CreatedUtc);
