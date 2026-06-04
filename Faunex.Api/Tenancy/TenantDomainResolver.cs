using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Tenancy;

public sealed class TenantDomainResolver(ApplicationDbContext db)
{
    public async Task<Guid?> ResolveTenantIdAsync(string? host, CancellationToken cancellationToken = default)
    {
        var hostname = NormalizeHost(host);
        if (hostname is null)
        {
            return null;
        }

        return await db.TenantDomains
            .Where(x => x.Hostname == hostname && x.IsActive && x.Tenant != null && x.Tenant.IsActive)
            .Select(x => (Guid?)x.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static string? NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var candidate = host.Trim();
        if (candidate.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                return null;
            }

            candidate = uri.Host;
        }
        else
        {
            candidate = candidate.Split('/')[0];
        }

        if (candidate.StartsWith("[", StringComparison.Ordinal))
        {
            var end = candidate.IndexOf("]", StringComparison.Ordinal);
            if (end < 0)
            {
                return null;
            }

            candidate = candidate[1..end];
        }
        else
        {
            var colon = candidate.LastIndexOf(':');
            if (colon >= 0)
            {
                candidate = candidate[..colon];
            }
        }

        candidate = candidate.Trim().TrimEnd('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
    }
}
