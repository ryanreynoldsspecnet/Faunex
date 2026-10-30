namespace Faunex.Domain.Entities;

public sealed class TenantChargeCatalog
{
    public Guid TenantId { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Guid WriteStamp { get; set; } = Guid.NewGuid();
    public bool IsPublished { get; set; }
    public string CurrencyCode { get; set; } = "ZAR";
    public string Terms { get; set; } = "";
    public string OptionsJson { get; set; } = "[]";
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
