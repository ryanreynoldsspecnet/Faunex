namespace Faunex.Application.DTOs;

public sealed record CreateBirdListingRequest(
    Guid SpeciesId,
    string Title,
    string? Description,
    decimal Price,
    bool IsAuction,
    Guid SellerId,
    string CurrencyCode = "ZAR",
    int Quantity = 1,
    string? Location = null,
    string? RingNumber = null,
    string? MicrochipNumber = null,
    string? Sex = null,
    DateOnly? HatchDate = null
);
