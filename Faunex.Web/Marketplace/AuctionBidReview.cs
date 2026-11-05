using Faunex.Application.DTOs;

namespace Faunex.Web.Marketplace;

public sealed record AuctionBidReview(Guid ListingId, Guid AuctionId, decimal Amount, decimal? LeadingAmount,
    decimal StartingPrice, decimal Increment, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt)
{
    public const decimal Maximum = 999999999m;
    public static decimal Minimum(PublicAuctionDto auction) => auction.CurrentPrice is { } price ? price + auction.MinimumIncrement : auction.StartingPrice;

    public static bool IsOpen(PublicAuctionDto auction, DateTimeOffset estimatedServerTime) => auction.State == "Live" && !auction.IsClosed
        && auction.StartsAt is { } start && start <= estimatedServerTime && auction.EndsAt is { } end && estimatedServerTime < end;

    public static AuctionBidReview Prepare(PublicAuctionDto auction, decimal amount, DateTimeOffset estimatedServerTime)
    {
        if (!IsOpen(auction, estimatedServerTime)) throw new InvalidOperationException("Bidding is not open. Refresh the auction for its latest status.");
        if (amount < 0.01m || amount < Minimum(auction) || amount > Maximum || decimal.Round(amount, 2) != amount)
            throw new InvalidOperationException("Enter a bid at or above the minimum, with no more than two decimal places.");
        return new(auction.ListingId, auction.Id, amount, auction.CurrentPrice, auction.StartingPrice, auction.MinimumIncrement, auction.StartsAt, auction.EndsAt);
    }

    public bool Matches(PublicAuctionDto auction, DateTimeOffset estimatedServerTime) => IsOpen(auction, estimatedServerTime)
        && auction.Id == AuctionId && auction.ListingId == ListingId && auction.CurrentPrice == LeadingAmount
        && auction.StartingPrice == StartingPrice && auction.MinimumIncrement == Increment && auction.StartsAt == StartsAt && auction.EndsAt == EndsAt
        && Amount >= Minimum(auction);
}
