namespace Faunex.Web.Marketplace;

public static class BuyerReturnRoute
{
    public static string? Validate(string? value)
    {
        if (value is null) return null;
        var parts = value.Split('/');
        return parts.Length == 3 && parts[0] == "" && parts[1] is "auctions" or "listings"
            && Guid.TryParseExact(parts[2], "D", out var id) ? $"/{parts[1]}/{id:D}" : null;
    }
}
