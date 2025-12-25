using StormBird.Domain.Entities;

namespace StormBird.Infrastructure.Repositories;

public interface IBirdListingRepository
{
    Task<BirdListing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(BirdListing entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(BirdListing entity, CancellationToken cancellationToken = default);
}
