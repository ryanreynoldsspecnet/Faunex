namespace Faunex.Application.DTOs;

public sealed record ListingDto(
    Guid Id,
    Guid TenantId,
    Guid SellerId,
    string AnimalClass,
    Guid? SpeciesId,
    string Title,
    string? Description,
    decimal StartingPrice,
    decimal? BuyNowPrice,
    string CurrencyCode,
    int Quantity,
    string? Location,
    bool IsActive
);
