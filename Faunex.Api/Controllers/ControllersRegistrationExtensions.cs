using Faunex.Api.Auth;
using Faunex.Api.Tenancy;
using Faunex.Application.Interfaces;
using Faunex.Application.Services;
using Faunex.Infrastructure.Persistence;
using Faunex.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Controllers;

public static class ControllersRegistrationExtensions
{
    public static IServiceCollection AddFaunexApiControllers(this IServiceCollection services)
    {
        services.AddControllers();

        services.AddHttpContextAccessor();

        services.AddScoped<ITenantContext, ClaimsTenantContext>();

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IAuctionService, AuctionService>();
        services.AddScoped<IBidService, BidService>();
        services.AddScoped<IBirdListingService, BirdListingService>();
        services.AddScoped<IListingQueryService, ListingQueryService>();
        services.AddScoped<IListingBrowseService, ListingBrowseService>();
        services.AddScoped<ISpeciesLookupService, SpeciesLookupService>();

        services.AddScoped<JwtTokenIssuer>();
        services.AddScoped<IdentitySeeder>();

        services.AddDbContext<ApplicationIdentityDbContext>((sp, options) =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var connectionString = cfg["ConnectionStrings:DefaultConnection"];
            options.UseNpgsql(connectionString);
        });

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
