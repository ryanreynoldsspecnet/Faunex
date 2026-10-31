using Faunex.Api.Auth;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Faunex.Api.Operations;

public sealed class DatabaseReadinessCheck(ApplicationDbContext application, ApplicationIdentityDbContext identity) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Exercise both schemas, not just the ability to open a database socket.
            await application.Tenants.IgnoreQueryFilters().AnyAsync(cancellationToken);
            await identity.Users.AnyAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Database readiness unavailable.");
        }
    }
}
