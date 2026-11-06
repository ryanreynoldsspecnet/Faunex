using System.Security.Claims;
using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers;

[ApiController, Route("api/auth/mfa"), Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin,PlatformComplianceAdmin,PlatformSupport,TenantAdmin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(8192)]
public sealed class AdminMfaController(UserManager<ApplicationUser> users, AdminMfaService mfa) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AdminMfaStatus>> Status() =>
        await users.GetUserAsync(User) is { } user ? Ok(mfa.Status(user)) : Unauthorized();

    [HttpPost("setup")]
    public async Task<ActionResult<AdminMfaSetup>> Setup(AdminMfaStart request, CancellationToken ct)
    {
        if (await users.GetUserAsync(User) is not { } user) return Unauthorized();
        var result = await mfa.BeginAsync(user, User, request.Password, ct);
        return result is null
            ? BadRequest(new { error = "Setup could not start. Sign in again and check your password. After repeated attempts, wait five minutes." })
            : Ok(result);
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<AdminMfaRecovery>> Confirm(AdminMfaConfirm request)
    {
        if (await users.GetUserAsync(User) is not { } user) return Unauthorized();
        var result = await mfa.ConfirmAsync(user, User, request);
        return result is null
            ? BadRequest(new { error = "The code or setup is invalid, expired, or temporarily locked." })
            : Ok(result);
    }
}
