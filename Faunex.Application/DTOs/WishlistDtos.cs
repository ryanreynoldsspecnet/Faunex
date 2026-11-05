namespace Faunex.Application.DTOs;

public sealed record BuyerWishlistDto(Guid Id, string Name, IReadOnlyList<Guid> ListingIds, int UnavailableCount);
public sealed record BuyerWishlistStateDto(Guid Revision, IReadOnlyList<BuyerWishlistDto> Lists);
public sealed record WishlistNameRequest(Guid Revision, string Name);
public sealed record WishlistMembershipRequest(Guid Revision, IReadOnlyList<Guid> WishlistIds);
public sealed record WishlistLotsDto(Guid Id, string Name, IReadOnlyList<MarketplaceLotDto> Items, int Total, int UnavailableCount, DateTimeOffset ServerTime);
