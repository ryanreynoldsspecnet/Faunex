namespace Faunex.Domain.Entities;

public sealed class ListingCompliance
{
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public Guid TenantId { get; set; }

    public ListingComplianceStatus Status { get; set; } = ListingComplianceStatus.Draft;

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }

    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
