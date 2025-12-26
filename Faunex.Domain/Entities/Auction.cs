using Faunex.Domain.Abstractions;
using Faunex.Domain.Enums;

namespace Faunex.Domain.Entities;

public sealed class Auction : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public AuctionType Type { get; set; }

    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    public decimal StartingPrice { get; set; }
    public decimal? ReservePrice { get; set; }
    public decimal? BuyNowPrice { get; set; }

    public bool IsSealedBid { get; set; }
    public bool IsClosed { get; set; }
}
