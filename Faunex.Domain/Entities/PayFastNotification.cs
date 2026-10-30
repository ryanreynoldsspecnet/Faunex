using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class PayFastNotification : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid AttemptId { get; set; }
    public string PayloadHash { get; set; } = "";
    public string ProviderPaymentId { get; set; } = "";
    public string ProviderStatus { get; set; } = "";
    public string Decision { get; set; } = "";
    public decimal Amount { get; set; }
    public int Number { get; set; }
}
