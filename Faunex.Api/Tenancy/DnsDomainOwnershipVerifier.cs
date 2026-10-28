using DnsClient;

namespace Faunex.Api.Tenancy;

public sealed class DnsDomainOwnershipVerifier(ILookupClient dns) : IDomainOwnershipVerifier
{
    public async Task<bool> VerifyAsync(string hostname, string token, CancellationToken cancellationToken)
    {
        try
        {
            var result = await dns.QueryAsync($"_faunex-verification.{hostname}", QueryType.TXT,
                cancellationToken: cancellationToken);
            return result.Answers.TxtRecords().Any(x => string.Equals(string.Concat(x.Text), $"faunex={token}", StringComparison.Ordinal));
        }
        catch (DnsResponseException)
        {
            return false;
        }
    }
}
