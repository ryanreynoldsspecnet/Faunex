using System.Net;
using Faunex.Shared;
using Faunex.Web.Operations;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Faunex.Tests;

public sealed class ReleaseReadinessTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";

    [Theory]
    [InlineData(null, "development")]
    [InlineData("", "development")]
    [InlineData("main\nsecret", "development")]
    [InlineData(Revision, Revision)]
    public void Release_marker_is_bounded(string? supplied, string expected) => Assert.Equal(expected, ReleaseIdentity.Normalize(supplied));

    [Theory]
    [InlineData("application")]
    [InlineData("identity")]
    public async Task Api_readiness_checks_both_database_schemas_and_does_not_disclose_failures(string schema)
    {
        await using var fixture = await AccountApiFixture.CreateAsync();
        var response = await fixture.Client.GetAsync("/api/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ready development", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        using var scope = fixture.Server.Services.CreateScope();
        var sql = schema == "application" ? "ALTER TABLE \"Tenants\" RENAME TO \"ReadinessTestTenants\""
            : "ALTER TABLE \"AspNetUsers\" RENAME TO \"ReadinessTestUsers\"";
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync(sql);
        response = await fixture.Client.GetAsync("/api/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("not ready", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(200, "ready 0123456789abcdef0123456789abcdef01234567", true)]
    [InlineData(200, "ready aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData(200, "<html>Login</html>", false)]
    [InlineData(302, "ready 0123456789abcdef0123456789abcdef01234567", false)]
    [InlineData(503, "database internal secret", false)]
    public async Task Web_requires_ready_api_at_the_same_revision(int status, string body, bool ready)
    {
        using var client = new HttpClient(new ProbeHandler(status, body)) { BaseAddress = new Uri("http://api.invalid") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ReleaseRevision"] = Revision }).Build();
        var result = await new ApiReadinessCheck(client, config).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(ready ? HealthStatus.Healthy : HealthStatus.Unhealthy, result.Status);
        Assert.Null(result.Exception);
        Assert.DoesNotContain("secret", result.Description ?? "");
    }

    [Fact]
    public async Task Web_fails_closed_on_network_errors()
    {
        using var client = new HttpClient(new ProbeHandler(0, "")) { BaseAddress = new Uri("http://api.invalid") };
        var result = await new ApiReadinessCheck(client, new ConfigurationBuilder().Build()).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Null(result.Exception);
    }

    private sealed class ProbeHandler(int status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("http://api.invalid/api/health/ready", request.RequestUri!.AbsoluteUri);
            if (status == 0) throw new HttpRequestException("test-only network failure containing secret");
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) });
        }
    }
}
