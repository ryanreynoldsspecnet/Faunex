using Faunex.Domain.Abstractions;

namespace Faunex.Domain.Entities;

public sealed class Listing : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid SellerId { get; set; }
    public User? Seller { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public decimal StartingPrice { get; set; }
    public decimal? BuyNowPrice { get; set; }

    public string CurrencyCode { get; set; } = "USD";

    public int Quantity { get; set; } = 1;
    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;

    public BirdDetails? BirdDetails { get; set; }
    public LivestockDetails? LivestockDetails { get; set; }
    public GameAnimalDetails? GameAnimalDetails { get; set; }
    public PoultryDetails? PoultryDetails { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();

    public ListingCompliance? Compliance { get; set; }
    public ICollection<ListingDocument> ComplianceDocuments { get; set; } = new List<ListingDocument>();
}
