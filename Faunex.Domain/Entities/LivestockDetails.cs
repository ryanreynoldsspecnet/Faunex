namespace Faunex.Domain.Entities;

public sealed class LivestockDetails
{
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string? Breed { get; set; }
}
