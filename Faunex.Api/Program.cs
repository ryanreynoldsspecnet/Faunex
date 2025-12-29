using System.Text;
using Faunex.Api.Controllers;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Faunex.Application.Auth;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFaunexApiControllers();

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var connectionString = cfg["ConnectionStrings:DefaultConnection"];
    options.UseNpgsql(connectionString);
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var issuer = builder.Configuration["FAUNEX_JWT_ISSUER"];
        var audience = builder.Configuration["FAUNEX_JWT_AUDIENCE"];
        var signingKey = builder.Configuration["FAUNEX_JWT_SIGNING_KEY"];

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(signingKey))
        {
            // DEV ONLY fallback.
            // IMPORTANT: Set FAUNEX_JWT_ISSUER / FAUNEX_JWT_AUDIENCE / FAUNEX_JWT_SIGNING_KEY in production.
            issuer = issuer ?? "faunex-dev";
            audience = audience ?? "faunex-dev";
            signingKey = signingKey ?? "DEV_ONLY_CHANGE_ME_DEV_ONLY_CHANGE_ME_DEV_ONLY_CHANGE_ME";
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtBearer")
                    .LogWarning(context.Exception, "JWT authentication failed");

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var principal = context.Principal;
                var actorId = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var tenantId = principal?.FindFirst(Faunex.Application.Auth.FaunexClaimTypes.TenantId)?.Value;
                var isPlatformAdmin = principal?.FindFirst(Faunex.Application.Auth.FaunexClaimTypes.IsPlatformAdmin)?.Value;

                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtBearer")
                    .LogInformation("JWT validated. actor_id={ActorId} tenant_id={TenantId} is_platform_admin={IsPlatformAdmin}", actorId, tenantId, isPlatformAdmin);

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BuyerOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole(FaunexRoles.Buyer));

    options.AddPolicy("SellerOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole(FaunexRoles.Seller));

    options.AddPolicy("PlatformSuperAdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole(FaunexRoles.PlatformAdmin, FaunexRoles.PlatformSuperAdmin));

    options.AddPolicy("PlatformCompliance", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole(FaunexRoles.PlatformAdmin)
            || ctx.User.IsInRole(FaunexRoles.PlatformSuperAdmin)
            || ctx.User.IsInRole(FaunexRoles.PlatformComplianceAdmin)
            || string.Equals(ctx.User.FindFirst(FaunexClaimTypes.IsPlatformAdmin)?.Value, "true", StringComparison.OrdinalIgnoreCase));
    });
});

var app = builder.Build();

app.Logger.LogInformation("API HOST STARTED. Environment={EnvironmentName}", app.Environment.EnvironmentName);
foreach (var url in app.Urls)
{
    app.Logger.LogInformation("API listening on {Url}", url);
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();

    // Add Identity tables (additive only)
    var identityDb = scope.ServiceProvider.GetRequiredService<Faunex.Api.Auth.ApplicationIdentityDbContext>();
    await identityDb.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var ex = exceptionHandlerPathFeature?.Error;

            if (ex != null)
            {
                app.Logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await Results.Problem(
                    title: "Unhandled exception",
                    detail: app.Environment.IsDevelopment() ? ex?.ToString() : null,
                    statusCode: StatusCodes.Status500InternalServerError,
                    instance: context.Request.Path)
                .ExecuteAsync(context);
        });
    });
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
