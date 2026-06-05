namespace Faunex.Web.Marketplace;

public sealed record MarketplaceContextDto(
    Guid TenantId,
    string Name,
    string? CompanyName,
    string? ContactEmail,
    string? ContactPhone,
    string? PrimaryDomain,
    IReadOnlyList<string> Domains);

public static class MarketplaceRoutes
{
    public static string HostQuery(string host) => Uri.EscapeDataString(host);
}
