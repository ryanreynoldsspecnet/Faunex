using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Faunex.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    JwtTokenIssuer tokenIssuer,
    IWebHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            TenantId = request.TenantId,
            IsPlatformAdmin = request.IsPlatformAdmin
        };

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToArray() });
        }

        if (environment.IsDevelopment())
        {
            // DEV ONLY: allow role assignment via request for local testing.
            foreach (var role in request.Roles.Distinct())
            {
                if (!FaunexRoles.All.Contains(role))
                {
                    return BadRequest(new { error = $"Unknown role '{role}'." });
                }

                if (!await roles.RoleExistsAsync(role))
                {
                    await roles.CreateAsync(new IdentityRole<Guid>(role));
                }

                await users.AddToRoleAsync(user, role);
            }
        }
        else
        {
            // TODO: In production, roles should be assigned by an admin workflow.
        }

        var (token, expiresAt, assignedRoles) = await tokenIssuer.IssueAsync(user, cancellationToken);

        logger.LogInformation("Registered user. actor_id={ActorId} tenant_id={TenantId} is_platform_admin={IsPlatformAdmin} roles={Roles}", user.Id, user.TenantId, user.IsPlatformAdmin, string.Join(',', assignedRoles));

        return Ok(new AuthResponse(
            AccessToken: token,
            ExpiresAt: expiresAt,
            ActorId: user.Id,
            Email: user.Email ?? request.Email,
            TenantId: user.TenantId,
            IsPlatformAdmin: user.IsPlatformAdmin,
            Roles: assignedRoles));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return BadRequest(new { error = "Invalid credentials." });
        }

        var ok = await users.CheckPasswordAsync(user, request.Password);
        if (!ok)
        {
            return BadRequest(new { error = "Invalid credentials." });
        }

        var (token, expiresAt, assignedRoles) = await tokenIssuer.IssueAsync(user, cancellationToken);

        logger.LogInformation("Login succeeded. actor_id={ActorId} tenant_id={TenantId} is_platform_admin={IsPlatformAdmin} roles={Roles}", user.Id, user.TenantId, user.IsPlatformAdmin, string.Join(',', assignedRoles));

        return Ok(new AuthResponse(
            AccessToken: token,
            ExpiresAt: expiresAt,
            ActorId: user.Id,
            Email: user.Email ?? request.Email,
            TenantId: user.TenantId,
            IsPlatformAdmin: user.IsPlatformAdmin,
            Roles: assignedRoles));
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<MeResponse> Me()
    {
        var actorIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _ = Guid.TryParse(actorIdRaw, out var actorId);

        var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var tenantId = IdentityTenantMapping.ResolveTenantId(User);
        var isPlatformAdmin = IdentityTenantMapping.ResolveIsPlatformAdmin(User);

        var roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();

        logger.LogInformation("/me resolved from claims. actor_id={ActorId} tenant_id={TenantId} is_platform_admin={IsPlatformAdmin} roles={Roles}", actorId, tenantId, isPlatformAdmin, string.Join(',', roles));

        return Ok(new MeResponse(actorId, email, tenantId, isPlatformAdmin, roles));
    }
}
