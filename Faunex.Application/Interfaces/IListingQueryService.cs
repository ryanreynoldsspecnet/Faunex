using Faunex.Application.DTOs;

namespace Faunex.Application.Interfaces;

public interface IListingQueryService
{
    Task<PagedResult<ListingDto>> BrowseAsync(ListingQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<ListingDto>> GetMyListingsAsync(Guid sellerId, ListingQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<ListingDto>> GetTenantListingsAsync(ListingQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<ListingDto>> GetAllListingsAsync(ListingQuery query, CancellationToken cancellationToken = default);
}
