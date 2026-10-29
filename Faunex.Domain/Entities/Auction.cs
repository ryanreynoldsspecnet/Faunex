using Faunex.Domain.Abstractions;
using Faunex.Domain.Enums;

namespace Faunex.Domain.Entities;

public sealed class Auction : BaseEntity
{
    public Guid Revision { get; set; } = Guid.NewGuid();
    public int RulesVersion { get; set; }
    public AuctionState State { get; set; }
    public decimal MinimumIncrement { get; set; } = 10m;
    public Guid? LeadingBidId { get; set; }
    public Guid? LeadingBidderId { get; set; }
    public decimal? LeadingAmount { get; set; }
    public int BidCount { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? Outcome { get; set; }
    public Guid? WinningBidId { get; set; }
    public Guid? WinnerId { get; set; }
    public string? CancellationReason { get; set; }
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
