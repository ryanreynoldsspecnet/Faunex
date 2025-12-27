using Faunex.Api.Auth;
using Faunex.Api.Tenancy;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Application.Services;
using Faunex.Infrastructure.Persistence;
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
        services.AddScoped<ISpeciesLookupService, NotImplementedSpeciesLookupService>();

        services.AddScoped<JwtTokenIssuer>();

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

    private sealed class NotImplementedSpeciesLookupService : ISpeciesLookupService
    {
        public Task<SpeciesDto?> FindByScientificNameAsync(string scientificName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<SpeciesDto?> GetByIdAsync(Guid speciesId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<SpeciesDto>> SearchAsync(string query, int take = 20, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
