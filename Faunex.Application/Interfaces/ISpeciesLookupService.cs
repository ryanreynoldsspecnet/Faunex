using StormBird.Application.DTOs;

namespace StormBird.Application.Interfaces;

public interface ISpeciesLookupService
{
    Task<SpeciesDto?> GetByIdAsync(Guid speciesId, CancellationToken cancellationToken = default);
    Task<SpeciesDto?> FindByScientificNameAsync(string scientificName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpeciesDto>> SearchAsync(string query, int take = 20, CancellationToken cancellationToken = default);
}
