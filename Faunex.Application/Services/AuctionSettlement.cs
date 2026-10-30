using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Services;

public sealed class AuctionSettlement(IApplicationDbContext db, TimeProvider clock)
{
    public Task<List<Guid>> DueAsync(CancellationToken ct) => db.Auctions.IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.RulesVersion == 1 && x.State == AuctionState.Scheduled && x.EndsAt <= clock.GetUtcNow())
        .OrderBy(x => x.EndsAt).Take(100).Select(x => x.Id).ToListAsync(ct);

    public async Task SettleAsync(Guid id, CancellationToken ct)
    {
        var auction = await db.Auctions.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (auction is null || auction.State != AuctionState.Scheduled) return;
        var listing = await db.Listings.IgnoreQueryFilters().SingleAsync(x => x.Id == auction.ListingId && x.TenantId == auction.TenantId, ct);
        await CompleteAsync(db, auction, listing, clock.GetUtcNow(), ct);
    }
    internal static async Task CompleteAsync(IApplicationDbContext db, Auction auction, Listing listing, DateTimeOffset now, CancellationToken ct)
    {
        if (auction.RulesVersion != 1 || auction.State != AuctionState.Scheduled || auction.EndsAt is null || now < auction.EndsAt)
            throw new ListingEditConflictException("A timed auction can only close after its scheduled end.");
        var bids = db.Bids.IgnoreQueryFilters().Where(x => x.TenantId == auction.TenantId && x.AuctionId == auction.Id);
        var leading = auction.LeadingBidId is { } id ? await bids.SingleOrDefaultAsync(x => x.Id == id, ct) : null;
        if (await bids.CountAsync(ct) != auction.BidCount
            || (auction.LeadingBidId != null && (leading is null || leading.Amount != auction.LeadingAmount || leading.CurrencyCode != auction.CurrencyCode || leading.BidderId != auction.LeadingBidderId || leading.BidNumber != auction.BidCount))
            || (leading is null && await bids.AnyAsync(ct)))
            throw new ListingValidationException("Auction bid state requires reconciliation; no result has been recorded.");
        var sold = leading is not null && (auction.ReservePrice is null || leading.Amount >= auction.ReservePrice);
        auction.Outcome = leading is null ? "NoBids" : sold ? "Sold" : "ReserveNotMet";
        auction.WinningBidId = sold ? leading!.Id : null; auction.WinnerId = sold ? leading!.BidderId : null;
        auction.State = AuctionState.Closed; auction.IsClosed = true; auction.ClosedAt = now;
        if (sold) db.AuctionOrders.Add(WinnerOrderFactory.Create(auction, listing, leading!, now));
        TimedAuctionRules.Touch(auction, listing, now);
        await db.SaveChangesAsync(ct);
    }
}
