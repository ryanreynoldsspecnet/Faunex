using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class ListingDocumentRequirement : BaseEntity
{
    public Guid TenantId { get; set; }
    public string AnimalClass { get; set; } = "bird";
    public ListingDocumentType DocumentType { get; set; }
}
