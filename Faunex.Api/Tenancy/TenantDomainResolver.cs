using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Faunex.Application.Tenancy;

namespace Faunex.Api.Tenancy;

public sealed class TenantDomainResolver(ApplicationDbContext db)
{
    public async Task<Guid?> ResolveTenantIdAsync(string? host, CancellationToken cancellationToken = default)
    {
        var hostname = NormalizeHost(host);
        if (hostname is null || !MarketplaceHosts.IsTenantDomain(hostname))
        {
            return null;
        }

        return await db.TenantDomains
            .Where(x => x.Hostname == hostname && x.IsActive && x.VerifiedAt != null && x.Tenant != null && x.Tenant.IsActive)
            .Select(x => (Guid?)x.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static string? NormalizeHost(string? host)
    {
        return MarketplaceHosts.Normalize(host);
    }
}
