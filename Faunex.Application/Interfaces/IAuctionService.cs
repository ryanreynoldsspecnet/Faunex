using Faunex.Application.DTOs;

namespace Faunex.Application.Interfaces;

public interface IAuctionService
{
    Task<AuctionDto?> GetByIdAsync(Guid auctionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuctionDto>> GetByListingIdAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(AuctionDto auction, CancellationToken cancellationToken = default);
    Task UpdateAsync(AuctionDto auction, CancellationToken cancellationToken = default);

    Task OpenAuctionAsync(Guid auctionId, CancellationToken cancellationToken = default);
    Task CloseAsync(Guid auctionId, CancellationToken cancellationToken = default);
}
