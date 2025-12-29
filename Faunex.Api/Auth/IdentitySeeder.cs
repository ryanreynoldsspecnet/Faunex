using Faunex.Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace Faunex.Api.Auth;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole<Guid>> roles,
    UserManager<ApplicationUser> users,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync(cancellationToken);
        await EnsurePlatformSuperAdminAsync(cancellationToken);
    }

    private async Task EnsureRolesAsync(CancellationToken cancellationToken)
    {
        var requiredRoles = new[]
        {
            FaunexRoles.Buyer,
            FaunexRoles.Seller,
            FaunexRoles.TenantAdmin,
            FaunexRoles.PlatformAdmin,
            FaunexRoles.PlatformSuperAdmin
        };

        foreach (var role in requiredRoles)
        {
            if (await roles.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roles.CreateAsync(new IdentityRole<Guid>(role));
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create role '{role}': {errors}");
            }
        }
    }

    private async Task EnsurePlatformSuperAdminAsync(CancellationToken cancellationToken)
    {
        const string email = "superadmin@faunex.co.za";

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                DisplayName = "Faunex Super Admin",
                TenantId = null,
                IsPlatformAdmin = true
            };

            var password = Environment.GetEnvironmentVariable("FAUNEX_SUPERADMIN_PASSWORD");
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("FAUNEX_SUPERADMIN_PASSWORD must be set to seed the platform super admin user.");
            }

            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                var errors = string.Join("; ", created.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create platform super admin user: {errors}");
            }

            logger.LogInformation("Seeded platform super admin user. email={Email} actor_id={ActorId}", email, user.Id);
        }

        if (user.IsPlatformAdmin != true || user.TenantId != null)
        {
            user.IsPlatformAdmin = true;
            user.TenantId = null;
            var updated = await users.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                var errors = string.Join("; ", updated.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to update platform super admin flags: {errors}");
            }
        }

        var requiredRoles = new[] { FaunexRoles.PlatformAdmin, FaunexRoles.PlatformSuperAdmin };
        var currentRoles = await users.GetRolesAsync(user);

        foreach (var role in requiredRoles)
        {
            if (currentRoles.Contains(role))
            {
                continue;
            }

            var add = await users.AddToRoleAsync(user, role);
            if (!add.Succeeded)
            {
                var errors = string.Join("; ", add.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign role '{role}' to platform super admin: {errors}");
            }
        }
    }
}
