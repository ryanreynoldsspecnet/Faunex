using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Faunex.Api.Controllers;
using Faunex.Infrastructure.Persistence;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];
    options.UseNpgsql(connectionString);
});

builder.Services.AddFaunexApiControllers();

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

builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(options =>
{
    const string schemeName = "Bearer";

    options.AddSecurityDefinition(schemeName, new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header
    });

    options.AddSecurityRequirement(_ => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference(schemeName)] = new List<string>()
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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
