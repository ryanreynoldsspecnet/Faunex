using System.Text.Json;
using Faunex.Application.DTOs;
using Faunex.Domain.Entities;

namespace Faunex.Application.Services;

public static class ChargePricing
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Read<T>(string value)
    {
        try { return JsonSerializer.Deserialize<T>(value, Json) ?? throw new ListingEditConflictException("Pricing terms require reconciliation."); }
        catch (JsonException) { throw new ListingEditConflictException("Pricing terms require reconciliation."); }
    }

    public static void Validate(string currency, string terms, IReadOnlyList<ChargeOptionDto> options, bool published)
    {
        if (currency is not ("ZAR" or "USD") || terms is null || terms.Length > 1000 || options is null || options.Count > 40)
            throw new ListingValidationException("Use ZAR or USD, terms up to 1000 characters and at most 40 options.");
        if (options.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 100 || x.Description is null || x.Description.Length > 500
            || x.Kind is not ("Delivery" or "RequiredExtra" or "OptionalExtra") || x.Amount < 0 || x.Amount > 999999999m || decimal.Round(x.Amount, 2) != x.Amount))
            throw new ListingValidationException("Each option needs a name, valid type and non-negative two-decimal per-lot price up to 999999999.");
        if (published && (string.IsNullOrWhiteSpace(terms) || !options.Any(x => x.IsActive && x.Kind == "Delivery")))
            throw new ListingValidationException("Published pricing needs terms and an active delivery or collection option. Free collection must be explicitly priced at zero.");
    }

    public static string? Freeze(TenantChargeCatalog? catalog, string currency)
    {
        if (catalog is null) return null; // Existing auction flow remains available, but checkout stays blocked without recorded terms.
        var options = Read<List<ChargeOptionDto>>(catalog.OptionsJson);
        if (!catalog.IsPublished || catalog.CurrencyCode != currency)
            throw new ListingEditConflictException("Publish charge options in the listing currency before publishing this auction.");
        Validate(catalog.CurrencyCode, catalog.Terms, options, true);
        catalog.WriteStamp = Guid.NewGuid();
        return Serialize(new ChargeTermsDto(catalog.Revision, currency, catalog.Terms, options.Where(x => x.IsActive).ToList()));
    }

    public static PriceBreakdownDto Calculate(AuctionOrder order, PriceSelectionRequest selection)
    {
        if (order.ChargeTermsJson is null) throw new ListingEditConflictException("No published pricing terms were recorded for this auction. Tenant review is required; later charges cannot be added retroactively.");
        var terms = Read<ChargeTermsDto>(order.ChargeTermsJson);
        Validate(terms.CurrencyCode, terms.Terms, terms.Options, true);
        if (terms.CurrencyCode != order.CurrencyCode || selection.CatalogRevision != terms.Revision)
            throw new ListingEditConflictException("The pricing selection does not match this order's recorded terms.");
        if (selection.OptionalChargeIds is null || selection.OptionalChargeIds.Count > 40
            || selection.OptionalChargeIds.Distinct().Count() != selection.OptionalChargeIds.Count)
            throw new ListingValidationException("Select each optional extra at most once.");
        var options = terms.Options.Where(x => x.IsActive).ToList();
        if (options.Any(x => x.Id == Guid.Empty) || options.Select(x => x.Id).Distinct().Count() != options.Count)
            throw new ListingEditConflictException("Recorded option identifiers require reconciliation.");
        var delivery = options.SingleOrDefault(x => x.Id == selection.DeliveryOptionId && x.Kind == "Delivery")
            ?? throw new ListingValidationException("Choose one recorded delivery or collection option.");
        var extras = options.Where(x => x.Kind == "OptionalExtra" && selection.OptionalChargeIds.Contains(x.Id)).ToList();
        if (extras.Count != selection.OptionalChargeIds.Count) throw new ListingValidationException("An optional extra is not available for this order.");
        var selected = options.Where(x => x.Kind == "RequiredExtra").Concat(new[] { delivery }).Concat(extras).ToList();
        var additional = selected.Sum(x => x.Amount);
        var total = order.Amount + additional;
        if (!TimedAuctionRules.Money(total)) throw new ListingValidationException("The total exceeds the supported checkout amount.");
        return new(terms.Revision, terms.CurrencyCode, terms.Terms, order.Amount,
            selected.Select(x => new PriceLineDto(x.Id, x.Name, x.Kind, x.Amount, x.Description)).ToList(), additional, total);
    }
    public static OrderQuoteDto Quote(OrderPriceQuote quote) => new(quote.Id, quote.OrderId, Read<PriceBreakdownDto>(quote.PricingJson), quote.CreatedAt);
}
