using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class ListingFile : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public Guid UploadedByUserId { get; set; }
    public bool IsPhoto { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public string OriginalSha256 { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
