using StormBird.Domain.Abstractions;
using StormBird.Domain.Enums;

namespace StormBird.Domain.Entities;

public sealed class Auction : BaseEntity
{
    public Guid ListingId { get; set; }
    public BirdListing? Listing { get; set; }

    public AuctionType Type { get; set; }

    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    public decimal StartingPrice { get; set; }
    public decimal? ReservePrice { get; set; }
    public decimal? BuyNowPrice { get; set; }

    public bool IsSealedBid { get; set; }
    public bool IsClosed { get; set; }
}
