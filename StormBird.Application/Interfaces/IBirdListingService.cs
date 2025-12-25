using StormBird.Application.DTOs;

namespace StormBird.Application.Interfaces;

public interface IBirdListingService
{
    Task<BirdListingDto?> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BirdListingDto>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(BirdListingDto listing, CancellationToken cancellationToken = default);
    Task UpdateAsync(BirdListingDto listing, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid listingId, CancellationToken cancellationToken = default);
}
