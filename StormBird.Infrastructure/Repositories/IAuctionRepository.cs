using StormBird.Domain.Entities;

namespace StormBird.Infrastructure.Repositories;

public interface IAuctionRepository
{
    Task<Auction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Auction entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Auction entity, CancellationToken cancellationToken = default);
}
