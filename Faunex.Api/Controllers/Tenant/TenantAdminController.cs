using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Faunex.Api.Controllers.Tenant;

[ApiController]
[Route("api/tenant")]
[Authorize(Policy = "TenantAdminOnly")]
public sealed class TenantAdminController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<TenantDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        if (tenantId is null)
        {
            return Forbid();
        }

        var tenant = await db.Tenants
            .Where(x => x.Id == tenantId.Value)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.CompanyName,
                x.IsActive,
                PrimaryDomain = x.Domains
                    .Where(d => d.IsPrimary && d.IsActive)
                    .Select(d => d.Hostname)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return NotFound(new { error = "Tenant not found." });
        }

        var userCount = await users.Users.CountAsync(x => x.TenantId == tenantId.Value, cancellationToken);
        var listingCount = await db.Listings.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId.Value, cancellationToken);
        var complianceQueueCount = await db.ListingCompliances
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId.Value, cancellationToken);

        return Ok(new TenantDashboardDto(
            tenant.Id,
            tenant.Name,
            tenant.CompanyName,
            tenant.PrimaryDomain,
            tenant.IsActive,
            userCount,
            listingCount,
            complianceQueueCount));
    }


    [HttpGet("branding")]
    public async Task<ActionResult<TenantBrandingDto>> GetBranding(CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        if (tenantId is null)
        {
            return Forbid();
        }

        var tenant = await db.Tenants
            .Where(x => x.Id == tenantId.Value)
            .Select(x => new TenantBrandingDto(
                x.Id,
                x.Name,
                x.CompanyName,
                x.MarketplaceDisplayName,
                x.MarketplaceTagline,
                x.LogoUrl,
                x.BrandPrimaryColor,
                x.SupportEmail,
                x.SupportPhone,
                x.ContactEmail,
                x.ContactPhone,
                x.Domains
                    .Where(d => d.IsPrimary && d.IsActive)
                    .Select(d => d.Hostname)
                    .FirstOrDefault(),
                x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return tenant is null ? NotFound(new { error = "Tenant not found." }) : Ok(tenant);
    }

    [HttpPut("branding")]
    public async Task<ActionResult<TenantBrandingDto>> UpdateBranding([FromBody] UpdateTenantBrandingRequest request, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        if (tenantId is null)
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(request.BrandPrimaryColor) && !IsHexColour(request.BrandPrimaryColor))
        {
            return BadRequest(new { error = "Primary brand colour must be a hex value like #1f6f4a." });
        }

        var tenant = await db.Tenants
            .Include(x => x.Domains)
            .FirstOrDefaultAsync(x => x.Id == tenantId.Value, cancellationToken);

        if (tenant is null)
        {
            return NotFound(new { error = "Tenant not found." });
        }

        tenant.MarketplaceDisplayName = Clean(request.MarketplaceDisplayName);
        tenant.MarketplaceTagline = Clean(request.MarketplaceTagline);
        tenant.LogoUrl = Clean(request.LogoUrl);
        tenant.BrandPrimaryColor = Clean(request.BrandPrimaryColor);
        tenant.SupportEmail = Clean(request.SupportEmail);
        tenant.SupportPhone = Clean(request.SupportPhone);
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToBrandingDto(tenant));
    }
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<TenantUserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        if (tenantId is null)
        {
            return Forbid();
        }

        var result = new List<TenantUserDto>();
        foreach (var user in await users.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value)
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken))
        {
            result.Add(await ToDtoAsync(user));
        }

        return Ok(result);
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<TenantUserDto>> GetUser(Guid userId)
    {
        var tenantId = CurrentTenantId();
        var user = await FindTenantUserAsync(userId, tenantId);
        if (user is null)
        {
            return NotFound(new { error = "User not found." });
        }

        return Ok(await ToDtoAsync(user));
    }

    [HttpPost("users")]
    public async Task<ActionResult<TenantUserDto>> CreateUser([FromBody] CreateTenantUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        if (tenantId is null)
        {
            return Forbid();
        }

        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required." });
        }

        var assignedRoles = NormalizeTenantRoles(request.Roles);
        if (assignedRoles.Count == 0)
        {
            return BadRequest(new { error = "At least one tenant role is required." });
        }

        var validation = await ValidateTenantAssignmentAsync(tenantId.Value, assignedRoles, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            return Conflict(new { error = "A user with this email already exists." });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = Clean(request.DisplayName),
            TenantId = tenantId.Value,
            IsPlatformAdmin = false
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return BadRequest(new { errors = created.Errors.Select(e => e.Description).ToArray() });
        }

        var roleResult = await SetRolesInternalAsync(user, assignedRoles);
        if (!roleResult.Succeeded)
        {
            await users.DeleteAsync(user);
            return BadRequest(new { errors = roleResult.Errors.Select(e => e.Description).ToArray() });
        }

        return CreatedAtAction(nameof(GetUser), new { userId = user.Id }, await ToDtoAsync(user));
    }

    [HttpPut("users/{userId:guid}")]
    public async Task<ActionResult<TenantUserDto>> UpdateUser(Guid userId, [FromBody] UpdateTenantUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        var user = await FindTenantUserAsync(userId, tenantId);
        if (user is null)
        {
            return NotFound(new { error = "User not found." });
        }

        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Email is required." });
        }

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null && existing.Id != userId)
        {
            return Conflict(new { error = "A user with this email already exists." });
        }

        var assignedRoles = NormalizeTenantRoles(request.Roles);
        if (assignedRoles.Count == 0)
        {
            return BadRequest(new { error = "At least one tenant role is required." });
        }

        var validation = await ValidateTenantAssignmentAsync(tenantId!.Value, assignedRoles, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        user.Email = email;
        user.UserName = email;
        user.DisplayName = Clean(request.DisplayName);
        user.TenantId = tenantId.Value;
        user.IsPlatformAdmin = false;
        user.LockoutEnd = request.IsActive ? null : DateTimeOffset.MaxValue;

        var roleResult = await SetRolesInternalAsync(user, assignedRoles);
        if (!roleResult.Succeeded)
        {
            return BadRequest(new { errors = roleResult.Errors.Select(e => e.Description).ToArray() });
        }

        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return BadRequest(new { errors = updated.Errors.Select(e => e.Description).ToArray() });
        }

        return Ok(await ToDtoAsync(user));
    }

    [HttpDelete("users/{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var currentActorRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(currentActorRaw, out var currentActorId) && currentActorId == userId)
        {
            return BadRequest(new { error = "You cannot delete your own account while logged in." });
        }

        var tenantId = CurrentTenantId();
        var user = await FindTenantUserAsync(userId, tenantId);
        if (user is null)
        {
            return NotFound(new { error = "User not found." });
        }

        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            return BadRequest(new { errors = deleted.Errors.Select(e => e.Description).ToArray() });
        }

        return NoContent();
    }

    private Guid? CurrentTenantId() => IdentityTenantMapping.ResolveTenantId(User);

    private async Task<ApplicationUser?> FindTenantUserAsync(Guid userId, Guid? tenantId)
    {
        if (tenantId is null)
        {
            return null;
        }

        var user = await users.FindByIdAsync(userId.ToString());
        return user?.TenantId == tenantId.Value ? user : null;
    }

    private async Task<ActionResult?> ValidateTenantAssignmentAsync(Guid tenantId, IReadOnlyCollection<string> assignedRoles, CancellationToken cancellationToken)
    {
        if (UserAccessRules.IsPlatformRoleSet(assignedRoles))
        {
            return BadRequest(new { error = "Tenant administrators cannot assign platform roles." });
        }

        var shape = UserAccessRules.ValidateRoleTenantShape(tenantId, assignedRoles);
        if (!shape.IsValid)
        {
            return BadRequest(new { error = shape.Error });
        }

        var tenantExists = await db.Tenants.AnyAsync(x => x.Id == tenantId && x.IsActive, cancellationToken);
        if (!tenantExists)
        {
            return BadRequest(new { error = "Tenant does not exist or is inactive." });
        }

        foreach (var role in assignedRoles)
        {
            if (await roles.RoleExistsAsync(role))
            {
                continue;
            }

            var created = await roles.CreateAsync(new IdentityRole<Guid>(role));
            if (!created.Succeeded)
            {
                return BadRequest(new { errors = created.Errors.Select(e => e.Description).ToArray() });
            }
        }

        return null;
    }

    private static IReadOnlyCollection<string> NormalizeTenantRoles(IReadOnlyList<string>? requestedRoles) =>
        UserAccessRules.NormalizeRoles(requestedRoles)
            .Where(x => !UserAccessRules.IsPlatformRole(x))
            .ToArray();

    private async Task<IdentityResult> SetRolesInternalAsync(ApplicationUser user, IReadOnlyCollection<string> assignedRoles)
    {
        var currentRoles = await users.GetRolesAsync(user);
        var remove = currentRoles.Except(assignedRoles, StringComparer.OrdinalIgnoreCase).ToArray();
        var add = assignedRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToArray();

        if (remove.Length > 0)
        {
            var removed = await users.RemoveFromRolesAsync(user, remove);
            if (!removed.Succeeded)
            {
                return removed;
            }
        }

        if (add.Length > 0)
        {
            return await users.AddToRolesAsync(user, add);
        }

        return IdentityResult.Success;
    }

    private async Task<TenantUserDto> ToDtoAsync(ApplicationUser user)
    {
        var assignedRoles = await users.GetRolesAsync(user);
        return new TenantUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            assignedRoles.ToArray(),
            IsActive: user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static TenantBrandingDto ToBrandingDto(Faunex.Domain.Entities.Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.CompanyName,
            tenant.MarketplaceDisplayName,
            tenant.MarketplaceTagline,
            tenant.LogoUrl,
            tenant.BrandPrimaryColor,
            tenant.SupportEmail,
            tenant.SupportPhone,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.Domains
                .Where(x => x.IsPrimary && x.IsActive)
                .Select(x => x.Hostname)
                .FirstOrDefault(),
            tenant.IsActive);

    private static bool IsHexColour(string value)
    {
        var candidate = value.Trim();
        if (candidate.Length is not (4 or 7) || candidate[0] != '#')
        {
            return false;
        }

        return candidate.Skip(1).All(Uri.IsHexDigit);
    }
}

public sealed record TenantDashboardDto(
    Guid TenantId,
    string TenantName,
    string? CompanyName,
    string? PrimaryDomain,
    bool IsActive,
    int UserCount,
    int ListingCount,
    int ComplianceQueueCount);


public sealed record TenantBrandingDto(
    Guid TenantId,
    string TenantName,
    string? CompanyName,
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? SupportEmail,
    string? SupportPhone,
    string? ContactEmail,
    string? ContactPhone,
    string? PrimaryDomain,
    bool IsActive);

public sealed record UpdateTenantBrandingRequest(
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? SupportEmail,
    string? SupportPhone);
public sealed record TenantUserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    bool IsActive);

public sealed record CreateTenantUserRequest(
    string Email,
    string Password,
    string? DisplayName = null,
    IReadOnlyList<string>? Roles = null);

public sealed record UpdateTenantUserRequest(
    string Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    bool IsActive);
