using System.Globalization;
using Faunex.Application.DTOs;
using Faunex.Application.Tenancy;

namespace Faunex.Web.Marketplace;

public static class StorefrontPresentation
{
    public static string Category(string value) => value switch
    { "bird" => "Birds", "livestock" => "Livestock", "game" => "Game animals", "poultry" => "Poultry", _ => "Other" };
    public static string Status(MarketplaceLotDto lot) => lot.Auction?.State switch
    {
        "Live" => "Bidding open", "Scheduled" => "Upcoming", "AwaitingResult" => "Awaiting result", "Cancelled" => "Cancelled",
        "Closed" => lot.Auction.Outcome switch { "Sold" => "Sold", "NoBids" => "No bids", "ReserveNotMet" => "Reserve not met", _ => "Closed" },
        _ => lot.FixedPrice.HasValue ? "Fixed price" : "Catalogue lot"
    };
    public static string PriceLabel(MarketplaceLotDto lot) => lot.FixedPrice.HasValue ? "Listed price"
        : lot.Auction?.CurrentPrice is not null ? lot.Auction.Outcome == "Sold" ? "Winning bid" : "Highest bid" : "Starting bid";
    public static string Money(decimal value, string currency) => $"{currency} {value.ToString("N2", CultureInfo.GetCultureInfo("en-ZA"))}";
    public static string Time(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(2)).ToString("dd MMM, HH:mm", CultureInfo.InvariantCulture) + " SAST";
    public static string BrandInk(string? colour)
    {
        if (!TenantBrandingRules.IsColour(colour)) return "#ffffff";
        static double Channel(string hex) { var v = Convert.ToInt32(hex, 16) / 255d; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        var luminance = 0.2126 * Channel(colour![1..3]) + 0.7152 * Channel(colour[3..5]) + 0.0722 * Channel(colour[5..7]);
        return luminance > 0.179 ? "#111111" : "#ffffff";
    }
}
