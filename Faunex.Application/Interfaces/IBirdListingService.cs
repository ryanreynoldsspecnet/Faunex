using Faunex.Application.DTOs;

namespace Faunex.Application.Interfaces;

public interface IBirdListingService
{
    Task<BirdListingDto?> GetByIdAsync(Guid listingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BirdListingDto>> GetBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingDto>> GetSellerListingsAsync(Guid sellerId, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(BirdListingDto listing, CancellationToken cancellationToken = default);
    Task UpdateAsync(BirdListingDto listing, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid listingId, CancellationToken cancellationToken = default);

    Task SubmitForReviewAsync(Guid listingId, CancellationToken cancellationToken = default);
    Task ApproveListingAsync(Guid listingId, string? notes, CancellationToken cancellationToken = default);
    Task RejectListingAsync(Guid listingId, string? notes, CancellationToken cancellationToken = default);
    Task SuspendListingAsync(Guid listingId, string? notes, CancellationToken cancellationToken = default);

    Task<ListingDto> CreateBirdListingAsync(CreateBirdListingRequest request, CancellationToken cancellationToken);
}
