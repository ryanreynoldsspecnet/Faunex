using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class OrderPriceQuote : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid AcceptedBy { get; set; }
    public string PricingJson { get; set; } = "";
}
