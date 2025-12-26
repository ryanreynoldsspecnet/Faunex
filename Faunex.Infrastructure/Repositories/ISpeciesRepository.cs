using StormBird.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StormBird.Infrastructure.Repositories;

public interface ISpeciesRepository
{
    Task<BirdSpecies?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BirdSpecies?> FindByScientificNameAsync(string scientificName, CancellationToken cancellationToken = default);
    Task AddAsync(BirdSpecies entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(BirdSpecies entity, CancellationToken cancellationToken = default);
}
