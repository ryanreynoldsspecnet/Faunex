using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class Bid : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid AuctionId { get; set; }
    public Auction? Auction { get; set; }

    public Guid BidderId { get; set; }
    public User? Bidder { get; set; }

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "USD";

    public DateTimeOffset PlacedAt { get; set; } = DateTimeOffset.UtcNow;
}
