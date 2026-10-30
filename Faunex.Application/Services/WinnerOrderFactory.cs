using Faunex.Application.DTOs;
using Faunex.Domain.Entities;

namespace Faunex.Application.Services;

public static class WinnerOrderFactory
{
    public static AuctionOrder Create(Auction auction, Listing listing, Bid bid, DateTimeOffset now)
    {
        if (auction.RulesVersion != 1 || auction.Type != Faunex.Domain.Enums.AuctionType.Timed || auction.IsSealedBid
            || auction.State != Faunex.Domain.Enums.AuctionState.Closed || !auction.IsClosed || auction.ClosedAt is null
            || auction.Outcome != "Sold" || auction.WinnerId != bid.BidderId || auction.LeadingBidderId != bid.BidderId
            || auction.WinningBidId != bid.Id || auction.LeadingBidId != bid.Id || auction.LeadingAmount != bid.Amount
            || bid.AuctionId != auction.Id || bid.TenantId != auction.TenantId || listing.Id != auction.ListingId
            || listing.TenantId != auction.TenantId || bid.CurrencyCode != auction.CurrencyCode || bid.BidNumber != auction.BidCount
            || (auction.ReservePrice is { } reserve && bid.Amount < reserve) || !TimedAuctionRules.Money(bid.Amount))
            throw new ListingEditConflictException("The auction result requires reconciliation before an order can be created.");
        return new AuctionOrder { TenantId = auction.TenantId, AuctionId = auction.Id, ListingId = listing.Id,
            WinningBidId = bid.Id, BuyerId = bid.BidderId, SellerId = listing.SellerId, Title = listing.Title,
            Quantity = listing.Quantity, Amount = bid.Amount, CurrencyCode = auction.CurrencyCode, ChargeTermsJson = auction.ChargeTermsJson,
            CreatedAt = now, UpdatedAt = now };
    }
}
