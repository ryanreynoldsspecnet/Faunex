using Faunex.Api.Tenancy;
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

    [HttpGet("tenants/{tenantId:guid}/domains")]
    public async Task<ActionResult<IReadOnlyList<TenantDomainDto>>> GetTenantDomains(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantExists = await db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            return NotFound(new { error = "Tenant not found." });
        }

        var domains = await db.TenantDomains
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Hostname)
            .Select(x => new TenantDomainDto(
                x.Id,
                x.TenantId,
                x.Hostname,
                x.IsPrimary,
                x.IsActive,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(domains);
    }

    [HttpPost("tenants/{tenantId:guid}/domains")]
    public async Task<ActionResult<TenantDomainDto>> CreateTenantDomain(
        Guid tenantId,
        [FromBody] CreateTenantDomainRequest request,
        CancellationToken cancellationToken)
    {
        var tenantExists = await db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            return NotFound(new { error = "Tenant not found." });
        }

        var hostname = TenantDomainResolver.NormalizeHost(request.Hostname);
        if (hostname is null)
        {
            return BadRequest(new { error = "A valid domain hostname is required." });
        }

        var exists = await db.TenantDomains.AnyAsync(x => x.Hostname == hostname, cancellationToken);
        if (exists)
        {
            return Conflict(new { error = "This domain is already assigned to a tenant." });
        }

        var now = DateTimeOffset.UtcNow;
        var domain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = hostname,
            IsPrimary = request.IsPrimary,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (domain.IsPrimary)
        {
            await db.TenantDomains
                .Where(x => x.TenantId == tenantId && x.IsPrimary)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.IsPrimary, false)
                    .SetProperty(x => x.UpdatedAt, now), cancellationToken);
        }

        db.TenantDomains.Add(domain);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetTenantDomains),
            new { tenantId },
            ToDomainDto(domain));
    }

    [HttpDelete("tenants/{tenantId:guid}/domains/{domainId:guid}")]
    public async Task<IActionResult> DeleteTenantDomain(Guid tenantId, Guid domainId, CancellationToken cancellationToken)
    {
        var domain = await db.TenantDomains
            .FirstOrDefaultAsync(x => x.Id == domainId && x.TenantId == tenantId, cancellationToken);

        if (domain is null)
        {
            return NotFound(new { error = "Domain not found." });
        }

        db.TenantDomains.Remove(domain);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static TenantDto ToDto(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            Slug: null,
            tenant.IsActive,
            tenant.CreatedAt);

    private static TenantDomainDto ToDomainDto(TenantDomain domain) =>
        new(
            domain.Id,
            domain.TenantId,
            domain.Hostname,
            domain.IsPrimary,
            domain.IsActive,
            domain.CreatedAt);
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

public sealed record CreateTenantDomainRequest(
    string Hostname,
    bool IsPrimary = false,
    bool IsActive = true);

public sealed record TenantDomainDto(
    Guid Id,
    Guid TenantId,
    string Hostname,
    bool IsPrimary,
    bool IsActive,
    DateTimeOffset CreatedUtc);
