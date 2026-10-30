using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class PayFastAttempt : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid SettingsRevision { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public string Environment { get; set; } = "Sandbox";
    public string MerchantId { get; set; } = "";
    public string ProtectedCredentials { get; set; } = "";
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "ZAR";
    public string ReturnUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public string NotifyUrl { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string? ProviderPaymentId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public int NotificationCount { get; set; }
}
