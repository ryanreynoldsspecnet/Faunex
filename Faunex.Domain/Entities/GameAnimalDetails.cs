namespace Faunex.Domain.Entities;

public sealed class GameAnimalDetails
{
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string? Species { get; set; }
}
