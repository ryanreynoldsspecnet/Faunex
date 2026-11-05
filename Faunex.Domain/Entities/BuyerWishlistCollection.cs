namespace Faunex.Domain.Entities;

public sealed class BuyerWishlistCollection
{
    public Guid TenantId { get; set; }
    public Guid BuyerId { get; set; }
    public Guid Revision { get; set; }
    public string ContentsJson { get; set; } = "[]";
}
