using Faunex.Domain.Enums;

namespace Faunex.Application.DTOs;

public sealed record AuctionDto(
    Guid Id,
    Guid ListingId,
    AuctionType Type,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    decimal StartingPrice,
    decimal? ReservePrice,
    decimal? BuyNowPrice,
    bool IsSealedBid,
    bool IsClosed,
    decimal MinimumIncrement = 10m,
    Guid? Revision = null,
    Guid? ListingRevision = null,
    string State = "Draft",
    decimal? CurrentPrice = null,
    string? Outcome = null,
    Guid? WinnerId = null,
    DateTimeOffset? ClosedAt = null,
    string? CurrencyCode = null
);
