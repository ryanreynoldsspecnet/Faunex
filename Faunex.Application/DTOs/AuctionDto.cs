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
    bool IsClosed
);
