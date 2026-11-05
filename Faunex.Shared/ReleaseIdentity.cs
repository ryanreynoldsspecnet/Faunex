using System.Text.RegularExpressions;

namespace Faunex.Shared;

public static partial class ReleaseIdentity
{
    public static string Normalize(string? value) => value is not null && Revision().IsMatch(value) ? value : "development";
    public static string Ready(string? value) => "ready " + Normalize(value);

    [GeneratedRegex("\\A[0-9a-f]{40}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Revision();
}
