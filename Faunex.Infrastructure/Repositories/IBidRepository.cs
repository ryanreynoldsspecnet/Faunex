using Faunex.Domain.Entities;

namespace Faunex.Infrastructure.Repositories;

public interface IBidRepository
{
    Task<Bid?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Bid entity, CancellationToken cancellationToken = default);
}
