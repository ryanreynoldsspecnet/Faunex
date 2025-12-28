using Faunex.Application.DTOs;

namespace Faunex.Application.Interfaces;

public interface IListingBrowseService
{
    Task<IReadOnlyList<ListingDto>> BrowseApprovedListingsAsync(CancellationToken cancellationToken);
    Task<ListingDto?> GetApprovedListingByIdAsync(Guid id, CancellationToken cancellationToken);
}
