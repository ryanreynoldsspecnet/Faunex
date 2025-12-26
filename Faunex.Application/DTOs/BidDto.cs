namespace Faunex.Application.DTOs;

public sealed record BidDto(
    Guid Id,
    Guid AuctionId,
    Guid BidderId,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset PlacedAt
);
