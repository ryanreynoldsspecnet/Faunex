namespace Faunex.Application.DTOs;

public sealed record BuyerBidActivityDto(
    Guid BidId, Guid ListingId, string? Title, decimal Amount, string CurrencyCode, DateTimeOffset PlacedAt,
    DateTimeOffset? EndsAt, string Status, bool IsWinningBid, bool AuctionAvailable, Guid? OrderId);
