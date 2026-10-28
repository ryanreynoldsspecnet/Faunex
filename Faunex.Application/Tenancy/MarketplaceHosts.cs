using System.Net;

namespace Faunex.Application.Tenancy;

public static class MarketplaceHosts
{
    public const string Root = "faunex.co.za";
    public const string OriginHeader = "X-Faunex-Marketplace-Host";
    private static readonly HashSet<string> ReservedLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "platform", "auth", "mail", "smtp", "imap", "pop", "ftp", "relay", "support", "status"
    };

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var input = value.Trim();
        if (input.Length > 2048 || input.Any(char.IsWhiteSpace)
            || !Uri.TryCreate(input.Contains("://", StringComparison.Ordinal) ? input : $"https://{input}", UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip)) return ip.ToString();
        return host.Length <= 253 && host.Split('.').All(IsLabel) ? host : null;
    }

    public static bool IsPlatform(string? host) => Normalize(host) is Root or "www.faunex.co.za"
        or "154.65.98.94" or "localhost" or "127.0.0.1" or "::1";

    public static bool IsTenantDomain(string? host)
    {
        var normalized = Normalize(host);
        if (normalized is null || IsPlatform(normalized) || !normalized.Contains('.')
            || IPAddress.TryParse(normalized, out _) || normalized.EndsWith(".localhost", StringComparison.Ordinal)
            || normalized.EndsWith(".local", StringComparison.Ordinal) || normalized.EndsWith(".internal", StringComparison.Ordinal)
            || normalized.EndsWith(".home.arpa", StringComparison.Ordinal)) return false;
        if (!normalized.EndsWith('.' + Root, StringComparison.Ordinal)) return true;
        var label = normalized[..^(Root.Length + 1)];
        return IsLabel(label) && !ReservedLabels.Contains(label);
    }

    public static bool IsManagedSubdomain(string host) => IsTenantDomain(host) && host.EndsWith('.' + Root, StringComparison.Ordinal);

    private static bool IsLabel(string label) => label.Length is > 0 and <= 63
        && label[0] != '-' && label[^1] != '-'
        && label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}
