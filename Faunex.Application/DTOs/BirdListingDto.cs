namespace Faunex.Application.DTOs;

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
    bool IsActive,
    string? RingNumber = null,
    string? MicrochipNumber = null,
    string? Sex = null,
    DateOnly? HatchDate = null,
    Guid? Revision = null,
    string? ComplianceStatus = null,
    bool HasAuctionActivity = false
);
