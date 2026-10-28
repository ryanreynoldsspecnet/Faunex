namespace Faunex.Domain.Entities;

public sealed class ListingReviewEvent
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public Guid Revision { get; set; }
    public ListingComplianceStatus? FromStatus { get; set; }
    public ListingComplianceStatus ToStatus { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorKind { get; set; } = "System";
    public string? Notes { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
