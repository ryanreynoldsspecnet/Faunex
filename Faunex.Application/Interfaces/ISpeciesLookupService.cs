using Faunex.Application.DTOs;

namespace Faunex.Application.Interfaces;

public interface ISpeciesLookupService
{
    Task<IReadOnlyList<SpeciesDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SpeciesDto?> GetByIdAsync(Guid speciesId, CancellationToken cancellationToken = default);
    Task<SpeciesDto?> FindByScientificNameAsync(string scientificName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpeciesDto>> SearchAsync(string query, int take = 20, CancellationToken cancellationToken = default);
}
