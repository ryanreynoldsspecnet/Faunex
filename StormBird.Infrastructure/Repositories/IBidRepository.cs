using StormBird.Domain.Entities;

namespace StormBird.Infrastructure.Repositories;

public interface IBidRepository
{
    Task<Bid?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Bid entity, CancellationToken cancellationToken = default);
}
