using Faunex.Application.DTOs;

namespace Faunex.Application.Interfaces;

public interface IBidService
{
    Task<BidDto> PlaceBidAsync(Guid auctionId, decimal amount, CancellationToken cancellationToken = default);
    Task<PagedResult<BidDto>> GetBidsForAuctionAsync(Guid auctionId, int skip, int take, CancellationToken cancellationToken = default);
    Task<decimal?> GetCurrentPriceAsync(Guid auctionId, CancellationToken cancellationToken = default);
}
