using Faunex.Application.DTOs;

namespace Faunex.Application.Interfaces;

public interface IListingBrowseService
{
    Task<IReadOnlyList<ListingDto>> BrowseApprovedListingsAsync(CancellationToken cancellationToken);
}
