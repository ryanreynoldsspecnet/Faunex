namespace Faunex.Application.DTOs;

public sealed record FixedPriceAvailabilityDto(Guid ListingId, Guid Revision, decimal Price, string CurrencyCode,
    int Quantity, bool Available, string? UnavailableReason = null);
public sealed record FixedPricePurchaseRequest(Guid Revision, decimal ExpectedPrice, string CurrencyCode);
public sealed record FixedPriceOfferRequest(Guid Revision, decimal Amount, string CurrencyCode);
public sealed record FixedPriceOrderReference(Guid OrderId);
public sealed record FixedPriceOfferDto(Guid Id, Guid ListingId, string Title, decimal Amount, string CurrencyCode,
    string Status, DateTimeOffset ExpiresAt, Guid Revision, Guid? OrderId, decimal? CounterAmount = null);
public sealed record FixedPriceOfferDecision(Guid Revision, string Decision, decimal? CounterAmount = null);
