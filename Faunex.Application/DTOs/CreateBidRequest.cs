namespace Faunex.Application.DTOs;

public sealed record CreateBidRequest(
    Guid ListingId,
    decimal Amount
);
