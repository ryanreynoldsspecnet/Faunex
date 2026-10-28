using Faunex.Api.Tenancy;
using Faunex.Api.Auth;
using Faunex.Application.Auth;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Mvc;
using Faunex.Application.Tenancy;
using System.Security.Cryptography;
using System.Data;

namespace Faunex.Api.Controllers.Platform;

[ApiController]
[Route("api/platform")]
[Authorize(Policy = "PlatformAdminOnly")]
public sealed class PlatformTenantsController(
    ApplicationDbContext db,
    ApplicationIdentityDbContext identityDb,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IDomainOwnershipVerifier ownership) : ControllerBase
{
    [HttpPost("tenants")]
    public async Task<ActionResult<TenantDto>> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (TenantBrandingRules.Validate(Clean(request.BrandPrimaryColor), Clean(request.LogoUrl)) is { } brandingError)
            return BadRequest(new { error = brandingError });
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

        var firstAdminEmail = Clean(request.FirstAdminEmail);
        if (string.IsNullOrWhiteSpace(firstAdminEmail) || string.IsNullOrWhiteSpace(request.FirstAdminPassword))
        {
            return BadRequest(new { error = "First tenant admin email and password are required." });
        }

        var existingAdmin = await users.FindByEmailAsync(firstAdminEmail);
        if (existingAdmin is not null)
        {
            return Conflict(new { error = "A user with the first tenant admin email already exists." });
        }

        var tenant = new Faunex.Domain.Entities.Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            CompanyName = Clean(request.CompanyName),
            RegistrationNumber = Clean(request.RegistrationNumber),
            VatNumber = Clean(request.VatNumber),
            ContactFirstName = Clean(request.ContactFirstName),
            ContactLastName = Clean(request.ContactLastName),
            ContactEmail = Clean(request.ContactEmail),
            ContactPhone = Clean(request.ContactPhone),
            PhysicalAddress = Clean(request.PhysicalAddress),
            PostalAddress = Clean(request.PostalAddress),
            ShippingAddress = Clean(request.ShippingAddress),
            MarketplaceDisplayName = Clean(request.MarketplaceDisplayName),
            MarketplaceTagline = Clean(request.MarketplaceTagline),
            LogoUrl = Clean(request.LogoUrl),
            BrandPrimaryColor = Clean(request.BrandPrimaryColor),
            SupportEmail = Clean(request.SupportEmail),
            SupportPhone = Clean(request.SupportPhone),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Identity and tenant records must commit on the same connection/transaction.
        identityDb.Database.SetDbConnection(db.Database.GetDbConnection());
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var identityTransaction = await identityDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        var roleResult = await EnsureRoleAsync(FaunexRoles.TenantAdmin);
        if (!roleResult.Succeeded)
        {
            return BadRequest(new { errors = roleResult.Errors.Select(e => e.Description).ToArray() });
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = firstAdminEmail,
            Email = firstAdminEmail,
            DisplayName = Clean(request.FirstAdminDisplayName),
            TenantId = tenant.Id,
            IsPlatformAdmin = false
        };

        var createdAdmin = await users.CreateAsync(admin, request.FirstAdminPassword);
        if (!createdAdmin.Succeeded)
        {
            return BadRequest(new { errors = createdAdmin.Errors.Select(e => e.Description).ToArray() });
        }

        var assignedRole = await users.AddToRoleAsync(admin, FaunexRoles.TenantAdmin);
        if (!assignedRole.Succeeded)
        {
            return BadRequest(new { errors = assignedRole.Errors.Select(e => e.Description).ToArray() });
        }

        await transaction.CommitAsync(cancellationToken);
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
                x.CompanyName,
                x.RegistrationNumber,
                x.VatNumber,
                x.ContactFirstName,
                x.ContactLastName,
                x.ContactEmail,
                x.ContactPhone,
                x.PhysicalAddress,
                x.PostalAddress,
                x.ShippingAddress,
                x.MarketplaceDisplayName,
                x.MarketplaceTagline,
                x.LogoUrl,
                x.BrandPrimaryColor,
                x.SupportEmail,
                x.SupportPhone,
                x.Domains
                    .Where(d => d.IsPrimary && d.IsActive && d.VerifiedAt != null)
                    .Select(d => d.Hostname)
                    .FirstOrDefault(),
                x.Domains.Count(d => d.IsActive),
                x.IsActive,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(tenants);
    }

    [HttpGet("tenants/{tenantId:guid}")]
    public async Task<ActionResult<TenantDto>> GetTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants
            .Where(x => x.Id == tenantId)
            .Select(x => new TenantDto(
                x.Id,
                x.Name,
                null,
                x.CompanyName,
                x.RegistrationNumber,
                x.VatNumber,
                x.ContactFirstName,
                x.ContactLastName,
                x.ContactEmail,
                x.ContactPhone,
                x.PhysicalAddress,
                x.PostalAddress,
                x.ShippingAddress,
                x.MarketplaceDisplayName,
                x.MarketplaceTagline,
                x.LogoUrl,
                x.BrandPrimaryColor,
                x.SupportEmail,
                x.SupportPhone,
                x.Domains
                    .Where(d => d.IsPrimary && d.IsActive && d.VerifiedAt != null)
                    .Select(d => d.Hostname)
                    .FirstOrDefault(),
                x.Domains.Count(d => d.IsActive),
                x.IsActive,
                x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return NotFound(new { error = "Tenant not found." });
        }

        return Ok(tenant);
    }

    [HttpPut("tenants/{tenantId:guid}")]
    public async Task<ActionResult<TenantDto>> UpdateTenant(Guid tenantId, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        if (TenantBrandingRules.Validate(Clean(request.BrandPrimaryColor), Clean(request.LogoUrl)) is { } brandingError)
            return BadRequest(new { error = brandingError });
        var tenant = await db.Tenants
            .Include(x => x.Domains)
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return NotFound(new { error = "Tenant not found." });
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "Tenant name is required." });
        }

        var exists = await db.Tenants.AnyAsync(x => x.Id != tenantId && x.Name == name, cancellationToken);
        if (exists)
        {
            return Conflict(new { error = "A tenant with this name already exists." });
        }

        tenant.Name = name;
        tenant.CompanyName = Clean(request.CompanyName);
        tenant.RegistrationNumber = Clean(request.RegistrationNumber);
        tenant.VatNumber = Clean(request.VatNumber);
        tenant.ContactFirstName = Clean(request.ContactFirstName);
        tenant.ContactLastName = Clean(request.ContactLastName);
        tenant.ContactEmail = Clean(request.ContactEmail);
        tenant.ContactPhone = Clean(request.ContactPhone);
        tenant.PhysicalAddress = Clean(request.PhysicalAddress);
        tenant.PostalAddress = Clean(request.PostalAddress);
        tenant.ShippingAddress = Clean(request.ShippingAddress);
        tenant.MarketplaceDisplayName = Clean(request.MarketplaceDisplayName);
        tenant.MarketplaceTagline = Clean(request.MarketplaceTagline);
        tenant.LogoUrl = Clean(request.LogoUrl);
        tenant.BrandPrimaryColor = Clean(request.BrandPrimaryColor);
        tenant.SupportEmail = Clean(request.SupportEmail);
        tenant.SupportPhone = Clean(request.SupportPhone);
        tenant.IsActive = request.IsActive;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(tenant));
    }

    [HttpDelete("tenants/{tenantId:guid}")]
    public async Task<IActionResult> DeleteTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return NotFound(new { error = "Tenant not found." });
        }

        var hasUsers = await users.Users.AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (hasUsers)
        {
            return Conflict(new { error = "Delete or reassign tenant users before deleting this tenant." });
        }

        var hasListings = await db.Listings.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (hasListings)
        {
            return Conflict(new { error = "This tenant has listing activity and cannot be deleted." });
        }

        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("tenants/{tenantId:guid}/domains")]
    public async Task<ActionResult<IReadOnlyList<TenantDomainDto>>> GetTenantDomains(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantExists = await db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            return NotFound(new { error = "Tenant not found." });
        }

        var domains = await db.TenantDomains.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Hostname)
            .ToListAsync(cancellationToken);

        return Ok(domains.Select(ToDomainDto).ToArray());
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
        if (hostname is null || !MarketplaceHosts.IsTenantDomain(hostname))
        {
            return BadRequest(new { error = "Enter a public hostname that is not reserved for Faunex." });
        }

        var exists = await db.TenantDomains.AnyAsync(x => x.Hostname == hostname, cancellationToken);
        if (exists)
        {
            return Conflict(new { error = "This domain is already assigned to a tenant." });
        }

        var now = DateTimeOffset.UtcNow;
        var managed = MarketplaceHosts.IsManagedSubdomain(hostname);
        if (request.IsPrimary && (!managed || !request.IsActive))
            return BadRequest(new { error = "Only an active, verified domain can be primary." });
        var domain = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Hostname = hostname,
            IsPrimary = request.IsPrimary,
            IsActive = request.IsActive,
            VerificationToken = managed ? null : NewVerificationToken(),
            VerificationRequestedAt = managed ? null : now,
            VerifiedAt = managed ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (domain.IsPrimary)
        {
            await db.TenantDomains
                .Where(x => x.TenantId == tenantId && x.IsPrimary)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.IsPrimary, false)
                    .SetProperty(x => x.UpdatedAt, now), cancellationToken);
        }

        db.TenantDomains.Add(domain);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "This hostname was assigned or changed by another operation. Refresh and try again." });
        }

        return CreatedAtAction(
            nameof(GetTenantDomains),
            new { tenantId },
            ToDomainDto(domain));
    }

    [HttpPut("tenants/{tenantId:guid}/domains/{domainId:guid}")]
    public async Task<ActionResult<TenantDomainDto>> UpdateTenantDomain(Guid tenantId, Guid domainId,
        [FromBody] UpdateTenantDomainRequest request, CancellationToken cancellationToken)
    {
        var domain = await db.TenantDomains.FirstOrDefaultAsync(x => x.Id == domainId && x.TenantId == tenantId, cancellationToken);
        if (domain is null) return NotFound();
        if (request.IsPrimary && (domain.VerifiedAt is null || !request.IsActive))
            return BadRequest(new { error = "Only an active, verified domain can be primary." });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (request.IsPrimary)
            await db.TenantDomains.Where(x => x.TenantId == tenantId && x.Id != domainId && x.IsPrimary)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPrimary, false), cancellationToken);
        domain.IsActive = request.IsActive;
        domain.IsPrimary = request.IsPrimary;
        domain.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Domain verification changed. Refresh and try again." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "A domain changed during this operation. Refresh and try again." });
        }
        return Ok(ToDomainDto(domain));
    }

    [HttpPost("tenants/{tenantId:guid}/domains/{domainId:guid}/challenge")]
    public async Task<ActionResult<TenantDomainDto>> StartDomainVerification(Guid tenantId, Guid domainId, CancellationToken cancellationToken)
    {
        var domain = await db.TenantDomains.FirstOrDefaultAsync(x => x.Id == domainId && x.TenantId == tenantId, cancellationToken);
        if (domain is null) return NotFound();
        if (domain.VerifiedAt is not null) return Conflict(new { error = "This domain is already verified." });
        domain.VerificationToken = NewVerificationToken();
        domain.VerificationRequestedAt = DateTimeOffset.UtcNow;
        domain.IsPrimary = false;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "This domain changed. Refresh and try again." }); }
        return Ok(ToDomainDto(domain));
    }

    [HttpPost("tenants/{tenantId:guid}/domains/{domainId:guid}/verify")]
    public async Task<ActionResult<TenantDomainDto>> VerifyTenantDomain(Guid tenantId, Guid domainId, CancellationToken cancellationToken)
    {
        var domain = await db.TenantDomains.FirstOrDefaultAsync(x => x.Id == domainId && x.TenantId == tenantId, cancellationToken);
        if (domain is null) return NotFound();
        if (!MarketplaceHosts.IsTenantDomain(domain.Hostname)) return BadRequest(new { error = "This hostname cannot be assigned to a tenant." });
        if (domain.VerifiedAt is not null) return Ok(ToDomainDto(domain));
        if (!MarketplaceHosts.IsManagedSubdomain(domain.Hostname)
            && (string.IsNullOrEmpty(domain.VerificationToken) || domain.VerificationRequestedAt is null
                || domain.VerificationRequestedAt < DateTimeOffset.UtcNow.AddDays(-7)))
            return BadRequest(new { error = "Generate a current DNS verification record first." });
        if (!MarketplaceHosts.IsManagedSubdomain(domain.Hostname)
            && !await ownership.VerifyAsync(domain.Hostname, domain.VerificationToken!, cancellationToken))
            return BadRequest(new { error = "The matching DNS TXT record is not visible yet." });
        domain.VerifiedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { error = "The verification record changed. Refresh and try again." }); }
        return Ok(ToDomainDto(domain));
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

    private static TenantDto ToDto(Faunex.Domain.Entities.Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            Slug: null,
            tenant.CompanyName,
            tenant.RegistrationNumber,
            tenant.VatNumber,
            tenant.ContactFirstName,
            tenant.ContactLastName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.PhysicalAddress,
            tenant.PostalAddress,
            tenant.ShippingAddress,
            tenant.MarketplaceDisplayName,
            tenant.MarketplaceTagline,
            tenant.LogoUrl,
            tenant.BrandPrimaryColor,
            tenant.SupportEmail,
            tenant.SupportPhone,
            tenant.Domains
                .Where(x => x.IsPrimary && x.IsActive && x.VerifiedAt != null)
                .Select(x => x.Hostname)
                .FirstOrDefault(),
            tenant.Domains.Count(x => x.IsActive),
            tenant.IsActive,
            tenant.CreatedAt);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<IdentityResult> EnsureRoleAsync(string role)
    {
        if (await roles.RoleExistsAsync(role))
        {
            return IdentityResult.Success;
        }

        return await roles.CreateAsync(new IdentityRole<Guid>(role));
    }

    private static TenantDomainDto ToDomainDto(TenantDomain domain) =>
        new(
            domain.Id,
            domain.TenantId,
            domain.Hostname,
            domain.IsPrimary,
            domain.IsActive,
            domain.CreatedAt,
            domain.VerifiedAt,
            domain.VerificationRequestedAt,
            string.IsNullOrEmpty(domain.VerificationToken) ? null : $"_faunex-verification.{domain.Hostname}",
            string.IsNullOrEmpty(domain.VerificationToken) ? null : $"faunex={domain.VerificationToken}");

    private static string NewVerificationToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}

public sealed record CreateTenantRequest(
    string Name,
    string? Slug = null,
    string? CompanyName = null,
    string? RegistrationNumber = null,
    string? VatNumber = null,
    string? ContactFirstName = null,
    string? ContactLastName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? PhysicalAddress = null,
    string? PostalAddress = null,
    string? ShippingAddress = null,
    string? MarketplaceDisplayName = null,
    string? MarketplaceTagline = null,
    string? LogoUrl = null,
    string? BrandPrimaryColor = null,
    string? SupportEmail = null,
    string? SupportPhone = null,
    bool IsActive = true,
    string? FirstAdminEmail = null,
    string? FirstAdminDisplayName = null,
    string? FirstAdminPassword = null);

public sealed record UpdateTenantRequest(
    string Name,
    string? Slug = null,
    string? CompanyName = null,
    string? RegistrationNumber = null,
    string? VatNumber = null,
    string? ContactFirstName = null,
    string? ContactLastName = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? PhysicalAddress = null,
    string? PostalAddress = null,
    string? ShippingAddress = null,
    string? MarketplaceDisplayName = null,
    string? MarketplaceTagline = null,
    string? LogoUrl = null,
    string? BrandPrimaryColor = null,
    string? SupportEmail = null,
    string? SupportPhone = null,
    bool IsActive = true);

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? Slug,
    string? CompanyName,
    string? RegistrationNumber,
    string? VatNumber,
    string? ContactFirstName,
    string? ContactLastName,
    string? ContactEmail,
    string? ContactPhone,
    string? PhysicalAddress,
    string? PostalAddress,
    string? ShippingAddress,
    string? MarketplaceDisplayName,
    string? MarketplaceTagline,
    string? LogoUrl,
    string? BrandPrimaryColor,
    string? SupportEmail,
    string? SupportPhone,
    string? PrimaryDomain,
    int DomainCount,
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
    DateTimeOffset CreatedUtc,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? VerificationRequestedAt,
    string? VerificationRecordName,
    string? VerificationRecordValue);

public sealed record UpdateTenantDomainRequest(bool IsPrimary, bool IsActive);
