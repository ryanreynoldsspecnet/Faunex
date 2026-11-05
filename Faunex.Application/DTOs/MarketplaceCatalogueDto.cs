namespace Faunex.Application.DTOs;

public sealed record MarketplaceLotDto(Guid Id, string Title, string Category, string? Location, int Quantity,
    decimal StartingPrice, decimal? FixedPrice, string CurrencyCode, Guid? PhotoId, PublicAuctionDto? Auction);

public sealed record MarketplaceCatalogueDto(IReadOnlyList<MarketplaceLotDto> Items, int Total, DateTimeOffset ServerTime);
