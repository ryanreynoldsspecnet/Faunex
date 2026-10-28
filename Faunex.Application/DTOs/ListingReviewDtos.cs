namespace Faunex.Application.DTOs;

public sealed record ReviewQueueRow(Guid Id, string TenantName, string SellerName, string Title,
    string Status, DateTimeOffset? SubmittedAt, int FileCount);
public sealed record ReviewQueueResult(IReadOnlyList<ReviewQueueRow> Items, int Total, int Skip, int Take);
public sealed record ReviewEventDto(long Id, string? FromStatus, string ToStatus, Guid Revision,
    Guid? ActorId, string ActorKind, string? Notes, DateTimeOffset OccurredAt);
public sealed record ListingReviewDto(ListingDto Listing, string TenantName, string SellerName, Guid Revision,
    string Status, string? ReviewNotes, bool CanSubmit, bool CanReview, bool HasAuctions, int FileCount,
    string? Species, string? RingNumber, string? MicrochipNumber, string? Sex, DateOnly? HatchDate,
    IReadOnlyList<string> RequiredDocuments, IReadOnlyList<ReviewEventDto> History);
