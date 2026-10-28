using Faunex.Api.Auth;
using Faunex.Api.Email;
using Faunex.Api.Tenancy;
using Faunex.Application.Interfaces;
using Faunex.Application.Services;
using Faunex.Infrastructure.Persistence;
using Faunex.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Faunex.Api.Files;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Faunex.Api.Controllers;

public static class ControllersRegistrationExtensions
{
    public static IServiceCollection AddFaunexApiControllers(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            options.Filters.Add<DomainAuthorizationExceptionFilter>();
            options.Filters.Add<TenantHostFilter>();
        });

        services.AddHttpContextAccessor();
        services.AddOptions<FileUploadOptions>().BindConfiguration("Files");
        services.AddSingleton<IListingFileStorage, LocalListingFileStorage>();
        services.AddSingleton<IUploadScanner, ClamAvUploadScanner>();
        services.AddSingleton<UploadContentValidator>();
        services.AddScoped<ListingFileService>();
        services.AddScoped<Faunex.Api.Compliance.ListingReviewQueries>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddConcurrencyLimiter("file-uploads", limiter => { limiter.PermitLimit = 4; limiter.QueueLimit = 0; limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; });
        });

        services.AddScoped<ITenantContext, ClaimsTenantContext>();

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IAuctionService, AuctionService>();
        services.AddScoped<IBidService, BidService>();
        services.AddScoped<IBirdListingService, BirdListingService>();
        services.AddScoped<IListingQueryService, ListingQueryService>();
        services.AddScoped<IListingBrowseService, ListingBrowseService>();
        services.AddScoped<ISpeciesLookupService, SpeciesLookupService>();

        services.AddScoped<JwtTokenIssuer>();
        services.AddScoped<TradingProfileService>();
        services.AddScoped<TenantDomainResolver>();
        services.AddScoped<TenantRequestContext>();
        services.AddSingleton<DnsClient.ILookupClient>(new DnsClient.LookupClient(new DnsClient.LookupClientOptions(
            System.Net.IPAddress.Parse("1.1.1.1"), System.Net.IPAddress.Parse("8.8.8.8"))
        {
            UseCache = false,
            Timeout = TimeSpan.FromSeconds(3),
            Retries = 1
        }));
        services.AddScoped<IDomainOwnershipVerifier, DnsDomainOwnershipVerifier>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<UserMutationGuard>();
        services.AddScoped<IdentityMutationTransactionFilter>();

        services.AddDbContext<ApplicationIdentityDbContext>((sp, options) =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var connectionString = cfg["ConnectionStrings:DefaultConnection"];
            options.UseNpgsql(connectionString);
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}
