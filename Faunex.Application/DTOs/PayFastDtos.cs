namespace Faunex.Application.DTOs;

public sealed record CheckoutFieldDto(string Name, string Value);
public sealed record PayFastCheckoutDto(Guid AttemptId, string ActionUrl, decimal Amount, string CurrencyCode,
    string MerchantId, IReadOnlyList<CheckoutFieldDto> Fields, string Environment = "Sandbox");
public sealed record PayFastEventDto(string ProviderPaymentId, string ProviderStatus, string Decision, DateTimeOffset ReceivedAt);
public sealed record PayFastSummaryDto(Guid AttemptId, string Environment, string Status, decimal Amount, string CurrencyCode,
    string MerchantId, string? ProviderPaymentId, DateTimeOffset? ConfirmedAt, IReadOnlyList<PayFastEventDto> Events);
