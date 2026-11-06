using Faunex.Api.Marketplace;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Controllers;

[ApiController, Route("api/buyer/bids"), Authorize(Policy = "BuyerOnly")]
public sealed class BuyerBidActivityController(ApplicationDbContext db, ITenantContext context,
    MarketplaceLotProjection marketplace, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PagedResult<BuyerBidActivityDto>>> Get(string status = "all",
        int skip = 0, int take = 25, CancellationToken ct = default)
    {
        if (context.IsPlatformAdmin || context.TenantId is not { } tenantId ||
            context is not ITenantContextWithActor { ActorId: { } buyerId })
            return Forbid();
        if (status is not ("all" or "progress" or "results"))
            throw new ListingValidationException("Choose a valid bid activity filter.");

        var bids = db.Bids.AsNoTracking().Where(x => x.TenantId == tenantId && x.BidderId == buyerId);
        var auctions = db.Auctions.AsNoTracking().Where(x => x.TenantId == tenantId);
        auctions = status switch
        {
            "progress" => auctions.Where(x => x.RulesVersion == 1 && x.State == AuctionState.Scheduled && x.StartsAt != null && x.EndsAt != null),
            "results" => auctions.Where(x => x.State == AuctionState.Closed || x.State == AuctionState.Cancelled),
            _ => auctions
        };
        var query = from bid in bids
                    join auction in auctions on bid.AuctionId equals auction.Id
                    select new { Bid = bid, Auction = auction };
        var total = await query.CountAsync(ct);
        if (total == 0) return new PagedResult<BuyerBidActivityDto>([], 0);
        var rows = await query.OrderByDescending(x => x.Bid.PlacedAt).ThenByDescending(x => x.Bid.BidNumber).ThenBy(x => x.Bid.Id)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 50)).ToListAsync(ct);
        if (rows.Count == 0) return new PagedResult<BuyerBidActivityDto>([], total);

        var listingIds = rows.Select(x => x.Auction.ListingId).Distinct().ToArray();
        var titles = await marketplace.Visible(tenantId).Where(x => listingIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Title, ct);
        var auctionIds = rows.Select(x => x.Auction.Id).Distinct().ToArray();
        var available = (await marketplace.PublishedAuctions(tenantId).Where(x => auctionIds.Contains(x.Id))
            .Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var wonIds = rows.Where(x => Won(x.Auction, buyerId)).Select(x => x.Auction.Id).Distinct().ToArray();
        var orders = await db.AuctionOrders.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BuyerId == buyerId && x.AuctionId.HasValue && wonIds.Contains(x.AuctionId.Value))
            .Select(x => new { x.AuctionId, x.ListingId, x.WinningBidId, x.Id, x.Title }).ToDictionaryAsync(x => x.AuctionId!.Value, ct);

        var now = clock.GetUtcNow();
        return new PagedResult<BuyerBidActivityDto>(rows.Select(row =>
        {
            var (bid, auction) = (row.Bid, row.Auction);
            var order = orders.GetValueOrDefault(auction.Id);
            if (order is not null && (order.ListingId != auction.ListingId || order.WinningBidId != auction.WinningBidId)) order = null;
            return new BuyerBidActivityDto(bid.Id, auction.ListingId, titles.GetValueOrDefault(auction.ListingId) ?? order?.Title,
                bid.Amount, bid.CurrencyCode, bid.PlacedAt, auction.EndsAt, Label(auction, buyerId, now),
                Won(auction, buyerId) && auction.WinningBidId == bid.Id,
                titles.ContainsKey(auction.ListingId) && available.Contains(auction.Id), order?.Id);
        }).ToList(), total);
    }

    private static bool Won(Auction auction, Guid buyerId) =>
        auction.State == AuctionState.Closed && auction.Outcome == "Sold" && auction.WinnerId == buyerId;

    private static string Label(Auction auction, Guid buyerId, DateTimeOffset now) => auction.State switch
    {
        AuctionState.Cancelled => "Cancelled",
        AuctionState.Closed => auction.Outcome switch
        {
            "Sold" => Won(auction, buyerId) ? "Won" : "NotWon",
            "ReserveNotMet" => "ReserveNotMet",
            "NoBids" => "NoBids",
            _ => "Closed"
        },
        _ => TimedAuctionRules.State(auction, now) switch
        {
            "Live" => auction.LeadingBidderId == buyerId ? "Leading" : "Outbid",
            "Scheduled" => "Scheduled",
            "AwaitingResult" => "AwaitingResult",
            _ => "Unavailable"
        }
    };
}
