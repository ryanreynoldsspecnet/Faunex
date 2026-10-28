using System.Text;
using Faunex.Application.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Faunex.Api.Auth;

public static class FaunexAuthenticationExtensions
{
    public static IServiceCollection AddFaunexAuthentication(this IServiceCollection services,
        IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddScoped<AccountAccessService>();
        services.AddScoped<AccountJwtBearerEvents>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = Required("FAUNEX_JWT_ISSUER", "faunex-dev"),
                ValidAudience = Required("FAUNEX_JWT_AUDIENCE", "faunex-dev"),
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    Required("FAUNEX_JWT_SIGNING_KEY", "DEV_ONLY_CHANGE_ME_DEV_ONLY_CHANGE_ME_DEV_ONLY_CHANGE_ME"))),
                ClockSkew = TimeSpan.FromMinutes(2)
            };
            options.EventsType = typeof(AccountJwtBearerEvents);
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("BuyerOnly", p => p.RequireAuthenticatedUser().RequireRole(FaunexRoles.Buyer));
            options.AddPolicy("SellerOnly", p => p.RequireAuthenticatedUser().RequireRole(FaunexRoles.Seller));
            options.AddPolicy("ListingManager", p => p.RequireAuthenticatedUser().RequireRole(FaunexRoles.Seller, FaunexRoles.TenantAdmin));
            options.AddPolicy("TenantAdminOnly", p => p.RequireAuthenticatedUser().RequireRole(FaunexRoles.TenantAdmin));
            options.AddPolicy("PlatformAdminOnly", p => p.RequireAuthenticatedUser().RequireRole(FaunexRoles.PlatformAdmin, FaunexRoles.PlatformSuperAdmin));
            options.AddPolicy("PlatformSuperAdminOnly", p => p.RequireAuthenticatedUser().RequireRole(FaunexRoles.PlatformSuperAdmin));
            options.AddPolicy("PlatformCompliance", p => p.RequireAuthenticatedUser().RequireRole(FaunexRoles.PlatformSuperAdmin, FaunexRoles.PlatformComplianceAdmin));
        });

        return services;

        string Required(string key, string fallback) => !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!
            : environment.IsDevelopment() ? fallback : throw new InvalidOperationException($"Missing required configuration value '{key}'.");
    }
}
