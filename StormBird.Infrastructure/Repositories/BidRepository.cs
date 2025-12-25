using Microsoft.EntityFrameworkCore;
using StormBird.Domain.Entities;
using StormBird.Infrastructure.Persistence;

namespace StormBird.Infrastructure.Repositories;

public sealed class BidRepository(ApplicationDbContext dbContext) : IBidRepository
{
    public Task<Bid?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Bids.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddAsync(Bid entity, CancellationToken cancellationToken = default)
    {
        dbContext.Bids.Add(entity);
        return Task.CompletedTask;
    }
}
