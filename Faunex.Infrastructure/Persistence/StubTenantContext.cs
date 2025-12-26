using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.Interfaces;
using System.Security.Claims;

namespace Faunex.Infrastructure.Persistence;

public sealed class StubTenantContext : ITenantContext, ITenantContextWithRoles, ITenantContextWithActor
{
    // This class is intentionally a stub.
    // Today: values are supplied directly (e.g., from tests or manual wiring).
    // Future: an auth-backed implementation will resolve these from the authenticated identity
    // (ClaimsPrincipal) and provide them to EF tenant scoping.

    public Guid? TenantId { get; init; }
    public bool IsPlatformAdmin { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    // Baseline ownership hook for service-layer authorization.
    // TODO: Replace with real authenticated user id.
    public Guid? ActorId { get; init; }

    public static StubTenantContext FromClaims(ClaimsPrincipal? principal)
    {
        // This method demonstrates the claims-to-tenant contract without enforcing it.
        // Controllers/services are NOT expected to use this yet; it exists as documentation
        // and a future integration point.
        // TODO: Replace with a real ITenantContext implementation once auth is introduced.

        var isPlatformAdmin = IdentityTenantMapping.ResolveIsPlatformAdmin(principal);

        return new StubTenantContext
        {
            IsPlatformAdmin = isPlatformAdmin,
            TenantId = isPlatformAdmin ? null : IdentityTenantMapping.ResolveTenantId(principal),
            Roles = IdentityTenantMapping.ResolveRoles(principal)
        };
    }
}
