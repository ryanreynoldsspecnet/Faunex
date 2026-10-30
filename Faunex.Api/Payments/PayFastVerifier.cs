using System.Net;
using System.Text;

namespace Faunex.Api.Payments;

public interface IPayFastVerifier
{
    bool IsSourceAllowed(IPAddress address);
    Task<bool> ValidateAsync(string canonical, CancellationToken ct);
}

public sealed class PayFastVerifier(HttpClient client) : IPayFastVerifier
{
    // Provider-published ranges, checked 30 October 2026. Fail closed; do not trust Referer/X-Forwarded-For.
    private static readonly IPNetwork[] Sources = [IPNetwork.Parse("197.97.145.144/28"), IPNetwork.Parse("41.74.179.192/27"),
        IPNetwork.Parse("102.216.36.0/28"), IPNetwork.Parse("102.216.36.128/28"), IPNetwork.Parse("144.126.193.139/32")];
    public bool IsSourceAllowed(IPAddress address) => Sources.Any(x => x.Contains(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address));
    public async Task<bool> ValidateAsync(string canonical, CancellationToken ct)
    {
        try
        {
            using var content = new StringContent(canonical, Encoding.UTF8, "application/x-www-form-urlencoded");
            using var response = await client.PostAsync(PayFastProtocol.ValidationUrl, content, ct);
            return response.IsSuccessStatusCode && (await response.Content.ReadAsStringAsync(ct)).Trim() == "VALID";
        }
        catch (HttpRequestException) { throw new PaymentUnavailableException("PayFast verification is temporarily unavailable."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PaymentUnavailableException("PayFast verification timed out."); }
    }
}
