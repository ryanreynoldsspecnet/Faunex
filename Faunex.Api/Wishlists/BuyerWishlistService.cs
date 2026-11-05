using System.Text.Json;
using Faunex.Api.Marketplace;
using Faunex.Application.Auth;
using Faunex.Application.Authorization;
using Faunex.Application.DTOs;
using Faunex.Application.Interfaces;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Api.Wishlists;

public sealed class BuyerWishlistService(ApplicationDbContext db, ITenantContext context, MarketplaceLotProjection lots, TimeProvider clock)
{
    public const int MaximumLists = 20, MaximumItemsPerList = 200, MaximumMemberships = 1000;
    private (Guid Tenant, Guid Buyer) Owner()
    {
        ServiceAuthorization.EnsureNotPlatformAdminForWrite(context, "access buyer wishlists");
        ServiceAuthorization.EnsureRole(context, FaunexRoles.Buyer);
        return (context.TenantId!.Value, (context as ITenantContextWithActor)?.ActorId ?? throw new UnauthorizedAccessException());
    }
    private async Task<BuyerWishlistCollection?> ReadAsync(CancellationToken ct)
    {
        var (tenant, buyer) = Owner();
        return await db.BuyerWishlistCollections.SingleOrDefaultAsync(x => x.TenantId == tenant && x.BuyerId == buyer, ct);
    }
    private static List<StoredWishlist> Contents(BuyerWishlistCollection? row) => row is null ? []
        : JsonSerializer.Deserialize<List<StoredWishlist>>(row.ContentsJson) ?? throw new InvalidOperationException("Wishlist data is unavailable.");
    private async Task<HashSet<Guid>> VisibleIdsAsync(Guid tenant, List<StoredWishlist> lists, CancellationToken ct)
    {
        var ids = lists.SelectMany(x => x.ListingIds).Distinct().ToArray();
        return (await lots.Visible(tenant).Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
    }
    private async Task<BuyerWishlistStateDto> StateAsync(BuyerWishlistCollection? row, CancellationToken ct)
    {
        var (tenant, _) = Owner(); var lists = Contents(row); var visible = await VisibleIdsAsync(tenant, lists, ct);
        return new(row?.Revision ?? Guid.Empty, lists.Select(x => new BuyerWishlistDto(x.Id, x.Name,
            x.ListingIds.Where(visible.Contains).ToArray(), x.ListingIds.Count(id => !visible.Contains(id)))).ToList());
    }
    public async Task<BuyerWishlistStateDto> GetAsync(CancellationToken ct) => await StateAsync(await ReadAsync(ct), ct);
    public async Task<WishlistLotsDto> GetLotsAsync(Guid id, int skip, int take, CancellationToken ct)
    {
        var (tenant, _) = Owner(); var lists = Contents(await ReadAsync(ct));
        var list = lists.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Wishlist not found.");
        var visible = await VisibleIdsAsync(tenant, [list], ct); var ids = list.ListingIds.AsEnumerable().Reverse().Where(visible.Contains).ToList();
        var now = clock.GetUtcNow(); var rows = await lots.ReadAsync(tenant, ids.Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 48)).ToList(), now, ct);
        return new(id, list.Name, rows, ids.Count, list.ListingIds.Count - ids.Count, now);
    }
    private async Task<BuyerWishlistStateDto> ChangeAsync(Guid revision, Func<List<StoredWishlist>, Task> change, CancellationToken ct)
    {
        var (tenant, buyer) = Owner(); var row = await ReadAsync(ct);
        if ((row?.Revision ?? Guid.Empty) != revision) throw new ListingEditConflictException("Your wishlists changed. Refresh them before saving again.");
        var lists = Contents(row); await change(lists);
        if (lists.Count > MaximumLists || lists.Any(x => x.ListingIds.Count > MaximumItemsPerList) || lists.Sum(x => x.ListingIds.Count) > MaximumMemberships)
            throw new ListingValidationException("Keep up to 20 wishlists, 200 lots in each and 1000 saved memberships in total.");
        if (row is null) { row = new() { TenantId = tenant, BuyerId = buyer }; db.BuyerWishlistCollections.Add(row); }
        row.ContentsJson = JsonSerializer.Serialize(lists); row.Revision = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        return await StateAsync(row, ct);
    }
    public Task<BuyerWishlistStateDto> NameAsync(Guid? id, WishlistNameRequest request, CancellationToken ct) => ChangeAsync(request.Revision, lists =>
    {
        var name = request.Name?.Trim().Normalize() ?? "";
        if (name.Length is < 1 or > 80 || name.Any(char.IsControl)) throw new ListingValidationException("Enter a wishlist name from 1 to 80 characters.");
        if (lists.Any(x => x.Id != id && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) throw new ListingValidationException("You already have a wishlist with that name.");
        if (id is null) lists.Add(new(Guid.NewGuid(), name, []));
        else { var index = lists.FindIndex(x => x.Id == id); if (index < 0) throw new KeyNotFoundException("Wishlist not found."); lists[index] = lists[index] with { Name = name }; }
        return Task.CompletedTask;
    }, ct);
    public Task<BuyerWishlistStateDto> DeleteAsync(Guid id, Guid revision, CancellationToken ct) => ChangeAsync(revision, lists =>
    {
        if (lists.RemoveAll(x => x.Id == id) == 0) throw new KeyNotFoundException("Wishlist not found.");
        return Task.CompletedTask;
    }, ct);
    public Task<BuyerWishlistStateDto> SetMembershipAsync(Guid listingId, WishlistMembershipRequest request, CancellationToken ct) => ChangeAsync(request.Revision, async lists =>
    {
        if (request.WishlistIds is null || request.WishlistIds.Count > MaximumLists || request.WishlistIds.Distinct().Count() != request.WishlistIds.Count
            || request.WishlistIds.Any(id => lists.All(x => x.Id != id))) throw new ListingValidationException("Choose your own wishlists.");
        var (tenant, _) = Owner();
        if (!await lots.Visible(tenant).AnyAsync(x => x.Id == listingId, ct)) throw new KeyNotFoundException("Lot not found or no longer available.");
        foreach (var list in lists)
        {
            if (request.WishlistIds.Contains(list.Id)) { if (!list.ListingIds.Contains(listingId)) list.ListingIds.Add(listingId); }
            else list.ListingIds.Remove(listingId);
        }
    }, ct);
    public Task<BuyerWishlistStateDto> ClearUnavailableAsync(Guid id, Guid revision, CancellationToken ct) => ChangeAsync(revision, async lists =>
    {
        var list = lists.SingleOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException("Wishlist not found.");
        var (tenant, _) = Owner(); var visible = await VisibleIdsAsync(tenant, [list], ct); list.ListingIds.RemoveAll(x => !visible.Contains(x));
    }, ct);

    private sealed record StoredWishlist(Guid Id, string Name, List<Guid> ListingIds);
}
