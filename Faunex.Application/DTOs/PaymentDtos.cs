namespace Faunex.Application.DTOs;

public sealed record PayFastSettingsDto(string MerchantId, bool HasCredentials, Guid Revision, DateTimeOffset? UpdatedAt,
    string Environment = "Sandbox", bool CheckoutAvailable = false);
public sealed record SavePayFastSettingsRequest(Guid Revision, string MerchantId, string? MerchantKey, string? Passphrase,
    string Environment = "Sandbox");
public sealed record AuctionOrderDto(Guid Id, Guid? AuctionId, Guid ListingId, string Title, int Quantity,
    decimal Amount, string CurrencyCode, DateTimeOffset CreatedAt, string Status = "AwaitingPayment", bool CheckoutAvailable = false,
    string PricingStatus = "ChargesNotFinalised", OrderQuoteDto? Quote = null, PayFastSummaryDto? Payment = null,
    bool Released = false, bool CanRelease = false)
{
    public string PaymentStage => Payment is not null ? Payment.Status switch
    {
        "Pending" => OrderPaymentStages.Pending,
        "SandboxComplete" => OrderPaymentStages.Confirmed,
        _ => OrderPaymentStages.Review
    } : Quote is null ? OrderPaymentStages.Pricing : OrderPaymentStages.Unpaid;
}

public static class OrderPaymentStages
{
    public const string Pricing = "pricing", Unpaid = "unpaid", Pending = "pending", Confirmed = "confirmed", Review = "review";
}
