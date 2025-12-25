using Microsoft.EntityFrameworkCore;
using StormBird.Domain.Entities;
using StormBird.Infrastructure.Persistence;

namespace StormBird.Infrastructure.Repositories;

public sealed class BirdListingRepository(ApplicationDbContext dbContext) : IBirdListingRepository
{
    public Task<BirdListing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.BirdListings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddAsync(BirdListing entity, CancellationToken cancellationToken = default)
    {
        dbContext.BirdListings.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BirdListing entity, CancellationToken cancellationToken = default)
    {
        dbContext.BirdListings.Update(entity);
        return Task.CompletedTask;
    }
}
