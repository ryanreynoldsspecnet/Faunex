namespace Faunex.Application.DTOs;

public sealed record PayFastSettingsDto(string MerchantId, bool HasCredentials, Guid Revision, DateTimeOffset? UpdatedAt,
    string Environment = "Sandbox", bool CheckoutAvailable = false);
public sealed record SavePayFastSettingsRequest(Guid Revision, string MerchantId, string? MerchantKey, string? Passphrase,
    string Environment = "Sandbox");
public sealed record AuctionOrderDto(Guid Id, Guid AuctionId, Guid ListingId, string Title, int Quantity,
    decimal Amount, string CurrencyCode, DateTimeOffset CreatedAt, string Status = "AwaitingPayment", bool CheckoutAvailable = false,
    string PricingStatus = "ChargesNotFinalised");
