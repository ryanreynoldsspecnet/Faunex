namespace Faunex.Domain.Entities;

public sealed class PoultryDetails
{
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string? Breed { get; set; }
}
