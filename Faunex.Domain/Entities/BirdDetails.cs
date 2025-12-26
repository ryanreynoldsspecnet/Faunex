namespace Faunex.Domain.Entities;

public sealed class BirdDetails
{
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public Guid? SpeciesId { get; set; }
    public BirdSpecies? Species { get; set; }
}
