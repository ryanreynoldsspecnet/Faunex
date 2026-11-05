namespace Faunex.Application.DTOs;

public sealed record AuctionCollectionOption(Guid Id, string Name);
public sealed record AuctionCollectionDto(Guid Id, string Name, bool IsActive, Guid Revision);
public sealed record CreateAuctionCollectionRequest(string Name);
public sealed record UpdateAuctionCollectionRequest(string Name, bool IsActive, Guid Revision);
public sealed record LotCollectionDto(Guid? CollectionId, Guid Revision, IReadOnlyList<AuctionCollectionDto> Collections);
public sealed record AssignLotCollectionRequest(Guid? CollectionId, Guid Revision);
