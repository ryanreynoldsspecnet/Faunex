using Microsoft.EntityFrameworkCore;
using StormBird.Domain.Entities;
using StormBird.Infrastructure.Persistence;

namespace StormBird.Infrastructure.Repositories;

public sealed class AuctionRepository(ApplicationDbContext dbContext) : IAuctionRepository
{
    public Task<Auction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Auctions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddAsync(Auction entity, CancellationToken cancellationToken = default)
    {
        dbContext.Auctions.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Auction entity, CancellationToken cancellationToken = default)
    {
        dbContext.Auctions.Update(entity);
        return Task.CompletedTask;
    }
}
