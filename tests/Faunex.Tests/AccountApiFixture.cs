using System.Net.Http.Headers;
using Faunex.Api.Auth;
using Faunex.Api.Tenancy;
using Faunex.Api.Email;
using Faunex.Api.Controllers;
using Faunex.Application.Auth;
using Faunex.Application.Tenancy;
using System.IdentityModel.Tokens.Jwt;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data.Common;
using Faunex.Api.Files;
using Faunex.Application.Interfaces;

namespace Faunex.Tests;

internal sealed class AccountApiFixture : IAsyncDisposable
{
    public const string Password = "Audit!Password123";
    public const string Issuer = "faunex-tests";
    public const string SigningKey = "FAUNEX_TEST_ONLY_SIGNING_KEY_NOT_FOR_PRODUCTION_123456789";
    private readonly DbConnection _keepAlive;
    private readonly WebApplication _host;
    private readonly string? _adminConnectionString;
    private readonly string? _databaseName;
    public bool UsesPostgres => _databaseName is not null;
    public TestServer Server { get; }
    public HttpClient Client { get; }

    private AccountApiFixture(DbConnection keepAlive, WebApplication host, string? adminConnectionString, string? databaseName)
    {
        _keepAlive = keepAlive;
        _host = host;
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        Server = host.GetTestServer();
        Client = Server.CreateClient();
    }

    public static async Task<AccountApiFixture> CreateAsync()
    {
        var connectionString = $"Data Source=faunex-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var postgres = Environment.GetEnvironmentVariable("FAUNEX_TEST_POSTGRES");
        string? databaseName = null;
        if (!string.IsNullOrWhiteSpace(postgres))
        {
            var settings = new NpgsqlConnectionStringBuilder(postgres);
            if (settings.Host is not ("localhost" or "127.0.0.1"))
                throw new InvalidOperationException("Integration tests only allow a local ephemeral PostgreSQL server.");
            databaseName = $"faunex_test_{Guid.NewGuid():N}";
            await using var admin = new NpgsqlConnection(postgres);
            await admin.OpenAsync();
            // The identifier is generated here, never accepted from a request or connection string.
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
            settings.Database = databaseName;
            settings.Pooling = false;
            connectionString = settings.ConnectionString;
        }
        DbConnection keepAlive = databaseName is null ? new SqliteConnection(connectionString) : new NpgsqlConnection(connectionString);
        await keepAlive.OpenAsync();
        var contentRoot = Path.Combine(Path.GetTempPath(), "faunex-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = contentRoot });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FAUNEX_JWT_ISSUER"] = Issuer,
            ["FAUNEX_JWT_AUDIENCE"] = Issuer,
            ["FAUNEX_JWT_SIGNING_KEY"] = SigningKey
        });
        builder.Logging.ClearProviders();
        var services = builder.Services;
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddFaunexApiControllers();
        services.AddSingleton<Faunex.Api.Payments.IPaymentSecretProtector>(new Faunex.Api.Payments.PaymentSecretProtector(new EphemeralDataProtectionProvider()));
        services.AddSingleton<TestAuctionClock>();
        services.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<TestAuctionClock>());
        services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        services.RemoveAll<DbContextOptions<ApplicationIdentityDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationIdentityDbContext>>();
        services.AddDbContext<ApplicationIdentityDbContext>(o =>
        {
            if (databaseName is null) o.UseSqlite(connectionString); else o.UseNpgsql(connectionString);
        });
        services.AddDbContext<ApplicationDbContext>(o =>
        {
            if (databaseName is null) o.UseSqlite(connectionString); else o.UseNpgsql(connectionString);
        });
        services.AddFaunexAuthentication(builder.Configuration, builder.Environment);
        services.AddSingleton<TestFileStorage>();
        services.AddSingleton<IListingFileStorage>(sp => sp.GetRequiredService<TestFileStorage>());
        services.AddSingleton<TestUploadScanner>();
        services.AddSingleton<IUploadScanner>(sp => sp.GetRequiredService<TestUploadScanner>());
        services.AddSingleton<TestDomainOwnership>();
        services.AddScoped<IDomainOwnershipVerifier>(sp => sp.GetRequiredService<TestDomainOwnership>());
        services.AddSingleton<TestEmailSender>();
        services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<TestEmailSender>());
        services.AddSingleton<OnboardingFailure>();
        services.AddScoped<UserManager<ApplicationUser>, TestUserManager>();
        var app = builder.Build();
        app.UseExceptionHandler(handler => handler.Run(context =>
        {
            context.Response.StatusCode = 500;
            return Task.CompletedTask;
        }));
        app.UseRouting();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        var fixture = new AccountApiFixture(keepAlive, app, postgres, databaseName);
        using var scope = app.Services.CreateScope();
        var application = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var identity = scope.ServiceProvider.GetRequiredService<ApplicationIdentityDbContext>();
        if (fixture.UsesPostgres)
        {
            await application.Database.MigrateAsync();
            await identity.Database.MigrateAsync();
        }
        else
        {
            await application.Database.ExecuteSqlRawAsync(application.Database.GenerateCreateScript());
            await identity.Database.ExecuteSqlRawAsync(identity.Database.GenerateCreateScript());
        }
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in FaunexRoles.All)
        {
            Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
        }
        return fixture;
    }

    public async Task<Guid> AddTenantAsync(bool active = true)
    {
        using var scope = Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = new Tenant { Name = Guid.NewGuid().ToString(), IsActive = active };
        db.Tenants.Add(tenant);
        db.TenantDomains.Add(new TenantDomain { TenantId = tenant.Id, Hostname = TenantHost(tenant.Id), VerifiedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        Client.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader);
        Client.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, TenantHost(tenant.Id));
        return tenant.Id;
    }

    public async Task<ApplicationUser> AddUserAsync(Guid? tenantId, params string[] roles)
    {
        using var scope = Server.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email,
            TenantId = tenantId, IsPlatformAdmin = UserAccessRules.IsPlatformRoleSet(roles) };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRolesAsync(user, roles)).Succeeded);
        return user;
    }

    public async Task<string> TokenAsync(ApplicationUser user)
    {
        using var scope = Server.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var current = await users.FindByIdAsync(user.Id.ToString());
        return (await scope.ServiceProvider.GetRequiredService<JwtTokenIssuer>().IssueAsync(current!)).token;
    }

    public HttpClient AuthenticatedClient(string token)
    {
        var client = Server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var tenant = new JwtSecurityTokenHandler().CanReadToken(token)
            ? new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.FirstOrDefault(x => x.Type == FaunexClaimTypes.TenantId)?.Value : null;
        client.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, Guid.TryParse(tenant, out var id) ? TenantHost(id) : MarketplaceHosts.Root);
        return client;
    }

    public static string TenantHost(Guid tenantId) => $"tenant-{tenantId:N}.faunex.co.za";

    public async Task ChangeUserAsync(Guid id, Func<UserManager<ApplicationUser>, ApplicationUser, Task> change)
    {
        using var scope = Server.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await change(users, (await users.FindByIdAsync(id.ToString()))!);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        var root = _host.Environment.ContentRootPath;
        await _host.DisposeAsync();
        var owned = Path.Combine(Path.GetTempPath(), "faunex-tests") + Path.DirectorySeparatorChar;
        if (Path.GetFullPath(root).StartsWith(owned, StringComparison.OrdinalIgnoreCase) && Directory.Exists(root)) Directory.Delete(root, true);
        await _keepAlive.DisposeAsync();
        if (_databaseName is not null && _adminConnectionString is not null)
        {
            await using var admin = new NpgsqlConnection(_adminConnectionString);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}

internal sealed class TestDomainOwnership : IDomainOwnershipVerifier
{
    public string? ExpectedToken { get; set; }
    public Func<Task<bool>>? Handler { get; set; }
    public Task<bool> VerifyAsync(string hostname, string token, CancellationToken cancellationToken = default) =>
        Handler is null ? Task.FromResult(token == ExpectedToken) : Handler();
}

internal sealed class TestEmailSender : IEmailSender
{
    public List<string> ResetLinks { get; } = [];
    public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        ResetLinks.Add(resetLink);
        return Task.CompletedTask;
    }
}

internal sealed class OnboardingFailure
{
    public bool ThrowAfterRoleAssignment { get; set; }
    public bool ReturnRoleFailure { get; set; }
}

internal sealed class TestUserManager(IUserStore<ApplicationUser> store, IOptions<IdentityOptions> options,
    IPasswordHasher<ApplicationUser> hasher, IEnumerable<IUserValidator<ApplicationUser>> userValidators,
    IEnumerable<IPasswordValidator<ApplicationUser>> passwordValidators, ILookupNormalizer normalizer,
    IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<ApplicationUser>> logger,
    OnboardingFailure failure)
    : UserManager<ApplicationUser>(store, options, hasher, userValidators, passwordValidators, normalizer, errors, services, logger)
{
    public override async Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role)
    {
        var result = await base.AddToRoleAsync(user, role);
        if (role == FaunexRoles.TenantAdmin && failure.ThrowAfterRoleAssignment)
        {
            throw new InvalidOperationException("Injected failure after role write.");
        }
        return role == FaunexRoles.TenantAdmin && failure.ReturnRoleFailure
            ? IdentityResult.Failed(new IdentityError { Description = "Injected role failure." }) : result;
    }
}
