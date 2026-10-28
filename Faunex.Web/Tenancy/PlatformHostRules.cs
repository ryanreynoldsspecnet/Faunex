namespace Faunex.Web.Tenancy;

public static class PlatformHostRules
{
    public static bool IsPlatformHost(string? host) =>
        Faunex.Application.Tenancy.MarketplaceHosts.IsPlatform(host);
}
