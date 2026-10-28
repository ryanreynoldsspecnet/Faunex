namespace Faunex.Domain.Entities;

public sealed class BirdDetails
{
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public Guid? SpeciesId { get; set; }
    public BirdSpecies? Species { get; set; }
    public string? RingNumber { get; set; }
    public string? MicrochipNumber { get; set; }
    public string? Sex { get; set; }
    public DateOnly? HatchDate { get; set; }
}
