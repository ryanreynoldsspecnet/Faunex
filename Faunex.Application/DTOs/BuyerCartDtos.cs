namespace Faunex.Application.DTOs;

public sealed record BuyerCartLotDto(Guid ListingId, string? Title, int Quantity, decimal? Price, string? CurrencyCode,
    Guid? ListingRevision, string? UnavailableReason);
public sealed record BuyerCartDto(Guid Revision, IReadOnlyList<BuyerCartLotDto> Lots, CartCheckoutResult? LatestCheckout);
public sealed record CartCheckoutLot(Guid ListingId, Guid ListingRevision, decimal ExpectedPrice, string CurrencyCode);
public sealed record CartCheckoutRequest(Guid Revision, IReadOnlyList<CartCheckoutLot> Lots);
public sealed record CartOrderDto(Guid OrderId, Guid ListingId, string Title, decimal Subtotal, string CurrencyCode);
public sealed record CartCheckoutResult(IReadOnlyList<CartOrderDto> Orders);
