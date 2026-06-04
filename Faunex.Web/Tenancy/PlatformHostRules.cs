namespace Faunex.Web.Tenancy;

public static class PlatformHostRules
{
    private static readonly HashSet<string> PlatformHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "faunex.co.za",
        "www.faunex.co.za",
        "154.65.98.94",
        "localhost",
        "127.0.0.1"
    };

    public static bool IsPlatformHost(string? host) =>
        !string.IsNullOrWhiteSpace(host) && PlatformHosts.Contains(host.Trim());
}
