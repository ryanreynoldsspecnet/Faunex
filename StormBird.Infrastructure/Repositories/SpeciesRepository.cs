using Microsoft.EntityFrameworkCore;
using StormBird.Domain.Entities;
using StormBird.Infrastructure.Persistence;

namespace StormBird.Infrastructure.Repositories;

public sealed class SpeciesRepository(ApplicationDbContext dbContext) : ISpeciesRepository
{
    public Task<BirdSpecies?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.BirdSpecies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<BirdSpecies?> FindByScientificNameAsync(string scientificName, CancellationToken cancellationToken = default) =>
        dbContext.BirdSpecies.FirstOrDefaultAsync(x => x.ScientificName == scientificName, cancellationToken);

    public Task AddAsync(BirdSpecies entity, CancellationToken cancellationToken = default)
    {
        dbContext.BirdSpecies.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BirdSpecies entity, CancellationToken cancellationToken = default)
    {
        dbContext.BirdSpecies.Update(entity);
        return Task.CompletedTask;
    }
}
