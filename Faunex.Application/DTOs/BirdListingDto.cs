namespace StormBird.Application.DTOs;

public sealed record BirdListingDto(
    Guid Id,
    Guid SellerId,
    Guid SpeciesId,
    string Title,
    string? Description,
    decimal StartingPrice,
    decimal? BuyNowPrice,
    string CurrencyCode,
    int Quantity,
    string? Location,
    bool IsActive
);
