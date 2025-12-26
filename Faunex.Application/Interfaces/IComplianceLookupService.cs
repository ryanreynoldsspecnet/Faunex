using Faunex.Domain.Enums;

namespace Faunex.Application.Interfaces;

public interface IComplianceLookupService
{
    Task<CITESAppendix> GetCitesAppendixAsync(Guid speciesId, CancellationToken cancellationToken = default);
    Task<bool> IsTradeAllowedAsync(Guid speciesId, string? originCountryCode, string? destinationCountryCode, CancellationToken cancellationToken = default);
}
