using Faunex.Shared;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Faunex.Web.Operations;

public sealed class ApiReadinessCheck(HttpClient client, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync("/api/health/ready", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return response.IsSuccessStatusCode && body == ReleaseIdentity.Ready(configuration["ReleaseRevision"])
                ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("API readiness unavailable.");
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("API readiness unavailable.");
        }
    }
}
