using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class AuctionOrder : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid AuctionId { get; set; }
    public Guid ListingId { get; set; }
    public Guid WinningBidId { get; set; }
    public Guid BuyerId { get; set; }
    public Guid SellerId { get; set; }
    public string Title { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "";
}
