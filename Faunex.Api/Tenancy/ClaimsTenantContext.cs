using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.Interfaces;
using System.Security.Claims;

namespace Faunex.Api.Tenancy;

public sealed class ClaimsTenantContext : ITenantContext, ITenantContextWithRoles, ITenantContextWithActor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ClaimsTenantContext> _logger;

    private bool _logged;

    public ClaimsTenantContext(IHttpContextAccessor httpContextAccessor, ILogger<ClaimsTenantContext> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    private bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? ActorId
    {
        get
        {
            if (!IsAuthenticated)
            {
                LogOnce();
                return null;
            }

            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? actorId = Guid.TryParse(raw, out var id) ? id : null;

            // TODO: If NameIdentifier is not a Guid, decide whether to support non-Guid actor ids.
            LogOnce(actorIdOverride: actorId);
            return actorId;
        }
    }

    public bool IsPlatformAdmin
    {
        get
        {
            if (!IsAuthenticated)
            {
                LogOnce();
                return false;
            }

            var isPlatformAdmin = IdentityTenantMapping.ResolveIsPlatformAdmin(Principal);
            LogOnce(isPlatformAdminOverride: isPlatformAdmin);
            return isPlatformAdmin;
        }
    }

    public Guid? TenantId
    {
        get
        {
            if (!IsAuthenticated)
            {
                LogOnce();
                return null;
            }

            var isPlatformAdmin = IdentityTenantMapping.ResolveIsPlatformAdmin(Principal);
            if (isPlatformAdmin)
            {
                LogOnce(isPlatformAdminOverride: true, tenantIdOverride: null);
                return null;
            }

            var tenantId = IdentityTenantMapping.ResolveTenantId(Principal);
            // TODO: If !isPlatformAdmin but tenantId is null, treat as mis-issued token.
            LogOnce(tenantIdOverride: tenantId);
            return tenantId;
        }
    }

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            if (!IsAuthenticated)
            {
                LogOnce();
                return Array.Empty<string>();
            }

            var roles = Principal!.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
            LogOnce(rolesCountOverride: roles.Length);
            return roles;
        }
    }

    private void LogOnce(Guid? actorIdOverride = null, Guid? tenantIdOverride = null, bool? isPlatformAdminOverride = null, int? rolesCountOverride = null)
    {
        if (_logged)
        {
            return;
        }

        _logged = true;

        var principal = Principal;
        Guid? actorId = actorIdOverride;
        if (actorId is null && principal?.Identity?.IsAuthenticated == true)
        {
            var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            actorId = Guid.TryParse(raw, out var id) ? id : null;
        }

        var isPlatformAdmin = isPlatformAdminOverride ?? IdentityTenantMapping.ResolveIsPlatformAdmin(principal);

        var tenantId = tenantIdOverride;
        if (tenantId is null && principal?.Identity?.IsAuthenticated == true && !isPlatformAdmin)
        {
            tenantId = IdentityTenantMapping.ResolveTenantId(principal);
        }

        var rolesCount = rolesCountOverride;
        if (rolesCount is null && principal?.Identity?.IsAuthenticated == true)
        {
            rolesCount = principal.FindAll(ClaimTypes.Role).Count();
        }

        _logger.LogInformation(
            "TenantContext resolved from claims. actor_id={ActorId} tenant_id={TenantId} is_platform_admin={IsPlatformAdmin} roles_count={RolesCount}",
            actorId,
            tenantId,
            isPlatformAdmin,
            rolesCount ?? 0);
    }
}
