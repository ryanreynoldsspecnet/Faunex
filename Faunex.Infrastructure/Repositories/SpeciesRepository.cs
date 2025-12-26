using Microsoft.EntityFrameworkCore;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;

namespace Faunex.Infrastructure.Repositories;

public sealed class SpeciesRepository(ApplicationDbContext dbContext) : ISpeciesRepository
{
    public Task<BirdSpecies?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.BirdSpeciesSet.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<BirdSpecies?> FindByScientificNameAsync(string scientificName, CancellationToken cancellationToken = default) =>
        dbContext.BirdSpeciesSet.FirstOrDefaultAsync(x => x.ScientificName == scientificName, cancellationToken);

    public Task AddAsync(BirdSpecies entity, CancellationToken cancellationToken = default)
    {
        dbContext.BirdSpeciesSet.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BirdSpecies entity, CancellationToken cancellationToken = default)
    {
        dbContext.BirdSpeciesSet.Update(entity);
        return Task.CompletedTask;
    }
}
