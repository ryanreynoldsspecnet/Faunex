using Faunex.Application.DTOs;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;

namespace Faunex.Application.Services;

public static class TimedAuctionRules
{
    public static void Touch(Auction auction, Listing listing, DateTimeOffset now)
    {
        auction.Revision = Guid.NewGuid(); listing.Revision = Guid.NewGuid();
        auction.UpdatedAt = listing.UpdatedAt = now;
    }
    public static bool Money(decimal amount) => amount is >= 0.01m and <= 999999999m && decimal.Round(amount, 2) == amount;
    public static void Approved(Listing listing)
    {
        if (!listing.IsActive || listing.Compliance?.Status != ListingComplianceStatus.Approved || listing.BuyNowPrice.HasValue)
            throw new ListingEditConflictException("An active, approved auction listing is required.");
    }
    public static void Revision(Auction auction, Guid? revision)
    {
        if (revision is null) throw new ListingValidationException("Reload the auction and send its current revision.");
        if (auction.Revision != revision) throw new ListingEditConflictException("The auction changed. Reload and try again.");
    }
    public static string State(Auction auction, DateTimeOffset now) => auction.RulesVersion != 1 || auction.StartsAt is null || auction.EndsAt is null ? "NeedsConfiguration"
        : auction.State == AuctionState.Scheduled ? now < auction.StartsAt ? "Scheduled" : now >= auction.EndsAt ? "AwaitingResult" : "Live"
        : auction.State.ToString();
    public static PublicAuctionDto Public(Auction a, DateTimeOffset now) => new(a.Id, a.ListingId, a.StartsAt, a.EndsAt,
        a.StartingPrice, a.MinimumIncrement, a.LeadingAmount, State(a, now), a.State is AuctionState.Closed or AuctionState.Cancelled || now >= a.EndsAt, a.Outcome, now);
}
