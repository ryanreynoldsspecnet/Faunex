using Faunex.Api.Marketplace;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Controllers;

[ApiController, Route("api/buyer/cart"), Authorize(Policy = "BuyerOnly")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None), RequestSizeLimit(16384)]
public sealed class BuyerCartController(ApplicationDbContext db, ITenantContext context, FixedPriceInventory inventory) : ControllerBase
{
    private const int Capacity = 20;

    [HttpGet]
    public async Task<BuyerCartDto> Get(CancellationToken ct)
    {
        var (tenant, buyer) = Owner();
        var row = await ReadAsync(tenant, buyer, ct);
        return await DescribeAsync(tenant, buyer, row, ct);
    }

    [HttpPut("lots/{id:guid}")]
    public async Task<BuyerCartDto> Add(Guid id, CancellationToken ct)
    {
        var (tenant, buyer) = Owner();
        var row = await ReadAsync(tenant, buyer, ct);
        var ids = Contents(row);
        if (ids.Contains(id)) return await DescribeAsync(tenant, buyer, row, ct);
        if (ids.Count >= Capacity) throw new ListingValidationException("Keep up to 20 whole lots in your cart.");
        var lot = await inventory.Lots(tenant).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (lot is null || !Buyable(lot, buyer) || await inventory.Reserved(tenant).AnyAsync(x => x.ListingId == id, ct))
            throw new KeyNotFoundException("This lot is not available for the cart.");
        if (!await inventory.CheckoutConfiguredAsync(tenant, "ZAR", ct)) throw FixedPriceInventory.CheckoutUnavailable();
        if (row is null) { row = new BuyerCart { TenantId = tenant, BuyerId = buyer }; db.BuyerCarts.Add(row); }
        ids.Add(id);
        SaveContents(row, ids);
        await db.SaveChangesAsync(ct);
        return await DescribeAsync(tenant, buyer, row, ct);
    }

    [HttpDelete("lots/{id:guid}")]
    public async Task<BuyerCartDto> Remove(Guid id, Guid revision, CancellationToken ct)
    {
        var (tenant, buyer) = Owner();
        var row = await ReadAsync(tenant, buyer, ct);
        if (row is null || row.Revision != revision) throw CartChanged();
        var ids = Contents(row);
        if (ids.Remove(id))
        {
            SaveContents(row, ids);
            await db.SaveChangesAsync(ct);
        }
        return await DescribeAsync(tenant, buyer, row, ct);
    }

    [HttpPost("checkout")]
    public async Task<CartCheckoutResult> Checkout(CartCheckoutRequest request, CancellationToken ct)
    {
        var (tenant, buyer) = Owner();
        if (request.Lots is null || request.Lots.Count is < 1 or > Capacity || request.Lots.Any(x => x is null))
            throw new ListingValidationException("Select one or more lots from your cart.");
        var reviewed = request.Lots.OrderBy(x => x.ListingId).ToArray();
        var selected = reviewed.Select(x => x.ListingId).ToArray();
        if (selected.Distinct().Count() != selected.Length) throw new ListingValidationException("Select each whole lot only once.");

        var row = await ReadAsync(tenant, buyer, ct) ?? throw CartChanged();
        var previous = Receipt(row);
        if (previous?.Revision == request.Revision)
        {
            if (!previous.Lots.SequenceEqual(reviewed)) throw CartChanged();
            return previous.Result;
        }
        var ids = Contents(row);
        if (row.Revision != request.Revision || selected.Any(id => !ids.Contains(id))) throw CartChanged();
        var lots = await inventory.Lots(tenant).AsTracking().Where(x => selected.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
        if (lots.Count != selected.Length || lots.Any(x => !Buyable(x, buyer))
            || await inventory.Reserved(tenant).AnyAsync(x => selected.Contains(x.ListingId), ct))
            throw new ListingEditConflictException("A selected lot is no longer available. Review your cart again.");
        var byId = lots.ToDictionary(x => x.Id);
        if (reviewed.Any(x => x.ListingRevision != byId[x.ListingId].Revision || x.ExpectedPrice != byId[x.ListingId].BuyNowPrice
            || x.CurrencyCode != byId[x.ListingId].CurrencyCode))
            throw new ListingEditConflictException("A selected lot changed. Review your cart again.");

        var charges = await inventory.FreezeChargesAsync(tenant, "ZAR", ct);
        var orders = lots.Select(x => inventory.Reserve(x, buyer, x.BuyNowPrice!.Value, null, charges))
            .Select(x => new CartOrderDto(x.Id, x.ListingId, x.Title, x.Amount, x.CurrencyCode)).ToArray();
        var result = new CartCheckoutResult(orders);
        SaveContents(row, ids.Where(id => !byId.ContainsKey(id)).ToList());
        row.LastCheckoutJson = ChargePricing.Serialize(new CheckoutReceipt(request.Revision, reviewed, result));
        // The cart, the receipt, and every order are committed in one EF transaction.
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<BuyerCartDto> DescribeAsync(Guid tenant, Guid buyer, BuyerCart? row, CancellationToken ct)
    {
        var ids = Contents(row);
        if (ids.Count == 0) return new(row?.Revision ?? Guid.Empty, [], Receipt(row)?.Result);
        var lots = await inventory.Lots(tenant).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var reserved = (await inventory.Reserved(tenant).Where(x => ids.Contains(x.ListingId)).Select(x => x.ListingId).ToListAsync(ct)).ToHashSet();
        var configured = await inventory.CheckoutConfiguredAsync(tenant, "ZAR", ct);
        return new(row!.Revision, ids.Select(id =>
        {
            if (!lots.TryGetValue(id, out var lot)) return new BuyerCartLotDto(id, null, 0, null, null, null, "Lot unavailable");
            var reason = !Buyable(lot, buyer) ? "Not available for online purchase" : reserved.Contains(id) ? "Lot reserved"
                : !configured ? "Checkout not configured" : null;
            return new BuyerCartLotDto(id, lot.Title, lot.Quantity, lot.BuyNowPrice, lot.CurrencyCode, lot.Revision, reason);
        }).ToList(), Receipt(row)?.Result);
    }

    private (Guid Tenant, Guid Buyer) Owner()
    {
        if (context.IsPlatformAdmin || context.TenantId is not { } tenant || context is not ITenantContextWithActor { ActorId: { } buyer })
            throw new UnauthorizedAccessException();
        return (tenant, buyer);
    }
    private Task<BuyerCart?> ReadAsync(Guid tenant, Guid buyer, CancellationToken ct) =>
        db.BuyerCarts.SingleOrDefaultAsync(x => x.TenantId == tenant && x.BuyerId == buyer, ct);
    private static bool Buyable(Listing lot, Guid buyer) => lot.SellerId != buyer && lot.CurrencyCode == "ZAR"
        && lot.BuyNowPrice is { } price && TimedAuctionRules.Money(price);
    private static List<Guid> Contents(BuyerCart? row) => row is null ? [] : ChargePricing.Read<List<Guid>>(row.ContentsJson);
    private static CheckoutReceipt? Receipt(BuyerCart? row) => row?.LastCheckoutJson is { } json ? ChargePricing.Read<CheckoutReceipt>(json) : null;
    private static void SaveContents(BuyerCart row, List<Guid> ids)
    {
        row.ContentsJson = ChargePricing.Serialize(ids);
        row.Revision = Guid.NewGuid();
    }
    private static ListingEditConflictException CartChanged() => new("Your cart changed. Refresh it before continuing.");
    private sealed record CheckoutReceipt(Guid Revision, IReadOnlyList<CartCheckoutLot> Lots, CartCheckoutResult Result);
}
