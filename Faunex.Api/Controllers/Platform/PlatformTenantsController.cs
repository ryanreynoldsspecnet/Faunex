using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Controllers.Platform;

[ApiController]
[Route("api/platform")]
[Authorize(Policy = "PlatformAdminOnly")]
public sealed class PlatformTenantsController(ApplicationDbContext db) : ControllerBase
{
    [HttpPost("tenants")]
    public async Task<ActionResult<TenantDto>> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "Tenant name is required." });
        }

        var exists = await db.Tenants.AnyAsync(x => x.Name == name, cancellationToken);
        if (exists)
        {
            return Conflict(new { error = "A tenant with this name already exists." });
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetTenants), new { id = tenant.Id }, ToDto(tenant));
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<IReadOnlyList<TenantDto>>> GetTenants(CancellationToken cancellationToken)
    {
        var tenants = await db.Tenants
            .OrderBy(x => x.Name)
            .Select(x => new TenantDto(
                x.Id,
                x.Name,
                null,
                x.IsActive,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(tenants);
    }

    private static TenantDto ToDto(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            Slug: null,
            tenant.IsActive,
            tenant.CreatedAt);
}

public sealed record CreateTenantRequest(
    string Name,
    string? Slug = null,
    bool IsActive = true);

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? Slug,
    bool IsActive,
    DateTimeOffset CreatedUtc);
