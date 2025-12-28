using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Infrastructure.Services;

public sealed class SpeciesLookupService(ApplicationDbContext dbContext) : ISpeciesLookupService
{
    public async Task<IReadOnlyList<SpeciesDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.BirdSpeciesSet
            .AsNoTracking()
            .OrderBy(x => x.CommonName)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<SpeciesDto?> GetByIdAsync(Guid speciesId, CancellationToken cancellationToken = default)
    {
        return await dbContext.BirdSpeciesSet
            .AsNoTracking()
            .Where(x => x.Id == speciesId)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SpeciesDto?> FindByScientificNameAsync(string scientificName, CancellationToken cancellationToken = default)
    {
        var term = scientificName.Trim();
        return await dbContext.BirdSpeciesSet
            .AsNoTracking()
            .Where(x => x.ScientificName == term)
            .Select(MapToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SpeciesDto>> SearchAsync(string query, int take = 20, CancellationToken cancellationToken = default)
    {
        var term = query.Trim();
        var size = take <= 0 ? 20 : Math.Min(take, 200);

        return await dbContext.BirdSpeciesSet
            .AsNoTracking()
            .Where(x => x.CommonName.Contains(term) || x.ScientificName.Contains(term))
            .OrderBy(x => x.CommonName)
            .Take(size)
            .Select(MapToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<BirdSpecies, SpeciesDto>> MapToDtoExpression() =>
        x => new SpeciesDto(
            x.Id,
            x.ScientificName,
            x.CommonName,
            x.CitesAppendix,
            x.IsEndangered,
            x.Notes);
}
