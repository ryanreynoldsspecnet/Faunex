using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class FixedPriceOffer : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid ListingId { get; set; }
    public Guid ListingRevision { get; set; }
    public Guid BuyerId { get; set; }
    public Guid SellerId { get; set; }
    public string Title { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? CounterAmount { get; set; }
    public string CurrencyCode { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}
