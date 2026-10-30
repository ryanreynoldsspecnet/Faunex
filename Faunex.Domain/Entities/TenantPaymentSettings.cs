namespace Faunex.Domain.Entities;

public sealed class TenantPaymentSettings
{
    public Guid TenantId { get; set; }
    public string MerchantId { get; set; } = "";
    public string ProtectedCredentials { get; set; } = "";
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
