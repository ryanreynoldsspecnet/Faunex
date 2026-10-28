using Faunex.Application.Auth;
using Faunex.Domain.Enums;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Auth;

public sealed class TradingProfileService(ApplicationDbContext db)
{
    public async Task SynchronizeAsync(ApplicationUser user, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        if (!user.TenantId.HasValue || user.IsPlatformAdmin)
        {
            return;
        }

        var role = roles.Contains(FaunexRoles.TenantAdmin) ? UserRole.Admin
            : roles.Contains(FaunexRoles.Seller) ? UserRole.Seller : UserRole.Buyer;
        var now = DateTimeOffset.UtcNow;
        var email = user.Email ?? string.Empty;
        var displayName = user.DisplayName ?? email;

        // Existing listing/bid FKs target this legacy profile, not AspNetUsers.
        // An atomic upsert supports simultaneous logins. Identity remains the authorization source.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "User" ("Id", "TenantId", "Email", "DisplayName", "Role", "IsActive", "CreatedAt", "UpdatedAt")
            VALUES ({user.Id}, {user.TenantId.Value}, {email}, {displayName}, {(int)role}, {true}, {now}, {now})
            ON CONFLICT ("Id") DO UPDATE SET
                "TenantId" = EXCLUDED."TenantId", "Email" = EXCLUDED."Email",
                "DisplayName" = EXCLUDED."DisplayName", "Role" = EXCLUDED."Role",
                "IsActive" = EXCLUDED."IsActive", "UpdatedAt" = EXCLUDED."UpdatedAt"
            """, cancellationToken);
    }
}
