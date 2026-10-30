namespace Faunex.Application.DTOs;

public sealed record ChargeOptionDto(Guid Id, string Name, string Kind, decimal Amount, string Description = "", bool IsActive = true);
public sealed record ChargeCatalogDto(Guid Revision, bool IsPublished, string CurrencyCode, string Terms, IReadOnlyList<ChargeOptionDto> Options);
public sealed record ChargeTermsDto(Guid Revision, string CurrencyCode, string Terms, IReadOnlyList<ChargeOptionDto> Options);
public sealed record PriceSelectionRequest(Guid CatalogRevision, Guid DeliveryOptionId, IReadOnlyList<Guid> OptionalChargeIds);
public sealed record AcceptPriceRequest(PriceSelectionRequest Selection, decimal ExpectedTotal);
public sealed record PriceLineDto(Guid OptionId, string Name, string Kind, decimal Amount, string Description);
public sealed record PriceBreakdownDto(Guid CatalogRevision, string CurrencyCode, string Terms, decimal Subtotal,
    IReadOnlyList<PriceLineDto> Lines, decimal AdditionalCharges, decimal Total);
public sealed record OrderQuoteDto(Guid Id, Guid OrderId, PriceBreakdownDto Pricing, DateTimeOffset AcceptedAt);
