using Microsoft.Extensions.Options;

namespace Faunex.Api.Payments;

public sealed class PayFastOptions
{
    public bool SandboxEnabled { get; set; }
    public string IngressToken { get; set; } = "";
}

public sealed class PayFastRuntime(IOptions<PayFastOptions> options)
{
    public const string PublicNotifyUrl = "https://faunex.co.za/payments/payfast/notify";
    public const string IngressHeader = "X-Faunex-PayFast-Ingress";
    public const string SourceHeader = "X-Faunex-PayFast-Source";
    public bool Ready => options.Value.SandboxEnabled && options.Value.IngressToken.Length >= 32;
    public string IngressToken => options.Value.IngressToken;
}

public sealed class PaymentUnavailableException(string message) : Exception(message);
public sealed class PaymentRejectedException(string message) : Exception(message);
