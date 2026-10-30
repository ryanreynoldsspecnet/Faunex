using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Faunex.Api.Tenancy;
using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Payments;

public sealed class PayFastService(ApplicationDbContext db, ITenantContext tenant, TenantRequestContext origin,
    IPaymentSecretProtector secrets, PayFastRuntime runtime, IPayFastVerifier verifier, TimeProvider clock)
{
    private sealed record Credentials(string MerchantKey, string Passphrase);
    private Credentials ReadSecrets(Guid id, string protectedValue)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Credentials>(secrets.Unprotect(id, protectedValue));
            if (string.IsNullOrWhiteSpace(value?.MerchantKey) || string.IsNullOrWhiteSpace(value.Passphrase)) throw new JsonException();
            return value;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        { throw new PaymentUnavailableException("Sandbox credentials could not be read. Re-enter them or restore the payment keys."); }
    }
    public bool CanCheckout(AuctionOrder order, OrderPriceQuote? quote, PayFastAttempt? attempt, TenantPaymentSettings? settings) =>
        runtime.Ready && quote is not null && order.CurrencyCode == "ZAR" && settings?.ProtectedCredentials.Length > 0
        && (attempt is null || (attempt.Status == "Pending" && attempt.SettingsRevision == settings.Revision));

    public async Task<AuctionOrder> OwnOrderAsync(Guid id, CancellationToken ct)
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(tenant, "pay tenant orders");
        ServiceAuthorization.EnsureRole(tenant, FaunexRoles.Buyer);
        var actor = (tenant as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException();
        return await db.AuctionOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.TenantId && x.BuyerId == actor, ct)
            ?? throw new KeyNotFoundException("Order not found.");
    }
    public async Task<PayFastCheckoutDto> PrepareAsync(Guid id, CancellationToken ct)
    {
        var order = await OwnOrderAsync(id, ct);
        if (!runtime.Ready) throw new PaymentUnavailableException("Sandbox checkout is not enabled or callback ingress is not configured.");
        var quote = await db.OrderPriceQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == id && x.TenantId == order.TenantId, ct)
            ?? throw new ListingEditConflictException("Accept the final itemised total before checkout.");
        var pricing = ChargePricing.Read<PriceBreakdownDto>(quote.PricingJson);
        if (order.CurrencyCode != "ZAR" || pricing.CurrencyCode != "ZAR") throw new ListingValidationException("PayFast checkout supports ZAR only. No currency conversion is performed.");
        if (pricing.Subtotal != order.Amount || pricing.Lines is null || pricing.Lines.Any(x => x.Amount < 0)
            || pricing.Lines.Sum(x => x.Amount) != pricing.AdditionalCharges || pricing.Subtotal + pricing.AdditionalCharges != pricing.Total
            || !TimedAuctionRules.Money(pricing.Total) || quote.AcceptedBy != order.BuyerId)
            throw new ListingEditConflictException("Accepted pricing requires reconciliation before payment.");
        var settings = await db.TenantPaymentSettings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == order.TenantId, ct);
        if (settings?.ProtectedCredentials.Length is not > 0) throw new PaymentUnavailableException("The tenant has not configured sandbox merchant credentials.");
        var attempt = await db.PayFastAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == id && x.TenantId == order.TenantId, ct);
        if (attempt is not null)
        {
            if (attempt.Status != "Pending") throw new ListingEditConflictException("This sandbox payment is confirmed or requires review. Do not pay it again.");
            if (attempt.SettingsRevision != settings.Revision) throw new ListingEditConflictException("The merchant settings changed after checkout was prepared. Tenant review is required.");
            return Form(attempt);
        }
        if (origin.Host is null || await origin.TenantIdAsync(ct) != order.TenantId) throw new UnauthorizedAccessException();
        ReadSecrets(order.TenantId, settings.ProtectedCredentials);
        var now = Now();
        attempt = new PayFastAttempt { TenantId = order.TenantId, OrderId = id, QuoteId = quote.Id,
            MerchantId = settings.MerchantId, ProtectedCredentials = settings.ProtectedCredentials, SettingsRevision = settings.Revision,
            Amount = pricing.Total, CurrencyCode = "ZAR", ReturnUrl = $"https://{origin.Host}/buyer/orders/{id}?payment=return",
            CancelUrl = $"https://{origin.Host}/buyer/orders/{id}?payment=cancel", NotifyUrl = PayFastRuntime.PublicNotifyUrl,
            CreatedAt = now, UpdatedAt = now };
        db.PayFastAttempts.Add(attempt); await db.SaveChangesAsync(ct);
        return Form(attempt);
    }
    private PayFastCheckoutDto Form(PayFastAttempt attempt)
    {
        var credentials = ReadSecrets(attempt.TenantId, attempt.ProtectedCredentials);
        List<CheckoutFieldDto> fields = [new("merchant_id", attempt.MerchantId), new("merchant_key", credentials.MerchantKey),
            new("return_url", attempt.ReturnUrl), new("cancel_url", attempt.CancelUrl), new("notify_url", attempt.NotifyUrl),
            new("m_payment_id", attempt.Id.ToString("N")), new("amount", attempt.Amount.ToString("0.00", CultureInfo.InvariantCulture)),
            new("item_name", "Faunex order " + attempt.OrderId.ToString("N"))];
        fields.Add(new("signature", PayFastProtocol.Signature(PayFastProtocol.Canonical(fields), credentials.Passphrase)));
        return new(attempt.Id, PayFastProtocol.CheckoutUrl, attempt.Amount, attempt.CurrencyCode, attempt.MerchantId, fields);
    }
    public static PayFastSummaryDto Summary(PayFastAttempt attempt, IReadOnlyList<PayFastEventDto>? events = null) => new(attempt.Id,
        attempt.Environment, attempt.Status, attempt.Amount, attempt.CurrencyCode, attempt.MerchantId, attempt.ProviderPaymentId, attempt.ConfirmedAt, events ?? []);

    public async Task NotifyAsync(IReadOnlyList<CheckoutFieldDto> fields, CancellationToken ct)
    {
        var data = fields.ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);
        if (!Guid.TryParseExact(data.GetValueOrDefault("m_payment_id"), "N", out var id)) throw new PaymentRejectedException("Invalid payment reference.");
        var attempt = await db.PayFastAttempts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new PaymentRejectedException("Unknown payment reference.");
        if (attempt.Environment != "Sandbox" || attempt.CurrencyCode != "ZAR") throw new PaymentRejectedException("Invalid payment environment.");
        var canonical = PayFastProtocol.Canonical(fields.Where(x => x.Name != "signature"));
        var credentials = ReadSecrets(attempt.TenantId, attempt.ProtectedCredentials);
        if (!PayFastProtocol.ValidSignature(PayFastProtocol.Signature(canonical, credentials.Passphrase), data.GetValueOrDefault("signature") ?? ""))
            throw new PaymentRejectedException("Invalid notification signature.");
        var providerId = data.GetValueOrDefault("pf_payment_id") ?? ""; var status = data.GetValueOrDefault("payment_status") ?? "";
        var gross = data.GetValueOrDefault("amount_gross") ?? "";
        if (data.GetValueOrDefault("merchant_id") != attempt.MerchantId || !Regex.IsMatch(providerId, "^[0-9]{1,32}$", RegexOptions.CultureInvariant)
            || !Regex.IsMatch(status, "^[A-Z_]{1,32}$", RegexOptions.CultureInvariant)
            || !Regex.IsMatch(gross, "^[0-9]{1,9}(\\.[0-9]{1,2})?$", RegexOptions.CultureInvariant)
            || !decimal.TryParse(gross, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount != attempt.Amount
            || (data.TryGetValue("currency", out var currency) && currency != "ZAR"))
            throw new PaymentRejectedException("Notification merchant, currency or gross amount does not match.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        if (await db.PayFastNotifications.IgnoreQueryFilters().AnyAsync(x => x.AttemptId == id && x.PayloadHash == hash, ct)) return;
        if (!await verifier.ValidateAsync(canonical, ct)) throw new PaymentRejectedException("PayFast did not validate this notification.");
        var decision = "NonCompletion";
        if (status == "COMPLETE")
        {
            var reused = await db.PayFastAttempts.IgnoreQueryFilters().AnyAsync(x => x.Id != id && x.MerchantId == attempt.MerchantId && x.ProviderPaymentId == providerId, ct);
            if (reused || (attempt.ProviderPaymentId is not null && attempt.ProviderPaymentId != providerId) || attempt.Status == "ReviewRequired")
            { attempt.Status = "ReviewRequired"; decision = "ReviewRequired"; }
            else
            {
                attempt.Status = "SandboxComplete"; attempt.ProviderPaymentId = providerId; attempt.ConfirmedAt ??= Now(); decision = "Confirmed";
            }
        }
        var now = Now(); attempt.Revision = Guid.NewGuid(); attempt.UpdatedAt = now; attempt.NotificationCount++;
        db.PayFastNotifications.Add(new PayFastNotification { TenantId = attempt.TenantId, AttemptId = id, Number = attempt.NotificationCount,
            PayloadHash = hash, ProviderPaymentId = providerId, ProviderStatus = status, Amount = amount, Decision = decision, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync(ct);
    }
    private DateTimeOffset Now() { var now = clock.GetUtcNow().ToUniversalTime(); return new(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero); }
}
