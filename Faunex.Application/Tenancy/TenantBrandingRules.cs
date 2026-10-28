namespace Faunex.Application.Tenancy;

public static class TenantBrandingRules
{
    public static bool IsColour(string? value) => value is { Length: 7 } && value[0] == '#' && value[1..].All(char.IsAsciiHexDigit);
    public static bool IsLogo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.Length > 2048 || value.Any(char.IsControl) || value.Contains('\\')) return false;
        if (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal)) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
    }
    public static string? Validate(string? colour, string? logo) => !string.IsNullOrWhiteSpace(colour) && !IsColour(colour)
        ? "Primary brand colour must be a hex value like #1f6f4a."
        : !IsLogo(logo) ? "Use an HTTPS logo URL or a local image path." : null;
}
