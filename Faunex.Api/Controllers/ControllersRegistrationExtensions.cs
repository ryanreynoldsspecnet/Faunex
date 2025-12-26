using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;

namespace Faunex.Api.Controllers;

public static class ControllersRegistrationExtensions
{
    public static IServiceCollection AddFaunexApiControllers(this IServiceCollection services)
    {
        services.AddControllers();

        services.AddScoped<IAuctionService, NotImplementedAuctionService>();
        services.AddScoped<IBirdListingService, NotImplementedBirdListingService>();
        services.AddScoped<ISpeciesLookupService, NotImplementedSpeciesLookupService>();

        return services;
    }

    private sealed class NotImplementedAuctionService : IAuctionService
    {
        public Task CloseAsync(Guid auctionId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Guid> CreateAsync(AuctionDto auction, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<AuctionDto?> GetByIdAsync(Guid auctionId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<AuctionDto>> GetByListingIdAsync(Guid listingId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateAsync(AuctionDto auction, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class NotImplementedBirdListingService : IBirdListingService
    {
        public Task<Guid> CreateAsync(BirdListingDto listing, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task DeactivateAsync(Guid listingId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<BirdListingDto?> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<BirdListingDto>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task UpdateAsync(BirdListingDto listing, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
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
