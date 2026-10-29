namespace Faunex.Application.DTOs;

public sealed record PublicAuctionDto(Guid Id, Guid ListingId, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt,
    decimal StartingPrice, decimal MinimumIncrement, decimal? CurrentPrice, string State, bool IsClosed,
    string? Outcome, DateTimeOffset ServerTime);
public sealed record PublicBidDto(decimal Amount, string CurrencyCode, DateTimeOffset PlacedAt);
