using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class Document : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string? ContentType { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public ComplianceStatus ComplianceStatus { get; set; } = ComplianceStatus.Unknown;
}
