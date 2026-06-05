namespace Faunex.Web.Marketplace;

public sealed record MarketplaceContextDto(
    Guid TenantId,
    string Name,
    string? CompanyName,
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? ContactEmail,
    string? ContactPhone,
    string? SupportEmail,
    string? SupportPhone,
    string? PrimaryDomain,
    IReadOnlyList<string> Domains);

public static class MarketplaceRoutes
{
    public static string HostQuery(string host) => Uri.EscapeDataString(host);
}
