namespace Faunex.Domain.Entities;

public sealed class AuctionCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public Guid Revision { get; set; } = Guid.NewGuid();
}
