namespace Faunex.Domain.Entities;

public sealed class BuyerCart
{
    public Guid TenantId { get; set; }
    public Guid BuyerId { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public string ContentsJson { get; set; } = "[]";
    public string? LastCheckoutJson { get; set; }
}
