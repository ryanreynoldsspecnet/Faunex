using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class BirdListing : BaseEntity
{
    public Guid SellerId { get; set; }

    public Guid SpeciesId { get; set; }
    public BirdSpecies? Species { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public decimal StartingPrice { get; set; }
    public decimal? BuyNowPrice { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public int Quantity { get; set; } = 1;
    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;
}
