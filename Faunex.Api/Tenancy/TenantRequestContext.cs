using Faunex.Application.Tenancy;

namespace Faunex.Api.Tenancy;

public sealed class TenantRequestContext(IHttpContextAccessor accessor, TenantDomainResolver domains)
{
    private Task<Guid?>? _tenant;
    public string? Host
    {
        get
        {
            var request = accessor.HttpContext?.Request;
            if (request is null) return null;
            if (!request.Headers.TryGetValue(MarketplaceHosts.OriginHeader, out var values))
                return MarketplaceHosts.Normalize(request.Host.Host);
            if (values.Count != 1) return null;
            var raw = values[0];
            var host = MarketplaceHosts.Normalize(raw);
            return string.Equals(host, raw?.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase) ? host : null;
        }
    }
    public bool IsPlatform => MarketplaceHosts.IsPlatform(Host);
    public Task<Guid?> TenantIdAsync(CancellationToken cancellationToken = default) =>
        _tenant ??= domains.ResolveTenantIdAsync(Host, cancellationToken);

    public async Task<bool> AllowsAccountAsync(Guid? tenantId, bool platform, bool tenantAdmin, CancellationToken cancellationToken)
    {
        if (IsPlatform) return platform || (tenantAdmin && tenantId.HasValue);
        return !platform && tenantId.HasValue && await TenantIdAsync(cancellationToken) == tenantId;
    }
}
