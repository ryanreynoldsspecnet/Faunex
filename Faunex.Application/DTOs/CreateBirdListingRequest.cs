namespace Faunex.Application.DTOs;

public sealed record CreateBirdListingRequest(
    Guid SpeciesId,
    string Title,
    string? Description,
    decimal Price,
    bool IsAuction,
    Guid SellerId
);
