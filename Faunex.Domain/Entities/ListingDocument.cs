using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class ListingDocument : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public ListingDocumentType DocumentType { get; set; } = ListingDocumentType.Unknown;

    public string? FileUrl { get; set; }

    public DateTimeOffset? UploadedAt { get; set; }

    public bool VerifiedByAdmin { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public Guid? VerifiedByUserId { get; set; }

    public string? Notes { get; set; }
}
