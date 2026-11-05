using Faunex.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Faunex.Api.Wishlists;

[ApiController, Route("api/buyer/wishlists"), Authorize(Policy = "BuyerOnly")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[RequestSizeLimit(16384)]
public sealed class BuyerWishlistsController(BuyerWishlistService wishlists) : ControllerBase
{
    [HttpGet] public Task<BuyerWishlistStateDto> Get(CancellationToken ct) => wishlists.GetAsync(ct);
    [HttpGet("{id:guid}/lots")] public Task<WishlistLotsDto> Lots(Guid id, int skip = 0, int take = 24, CancellationToken ct = default) => wishlists.GetLotsAsync(id, skip, take, ct);
    [HttpPost] public Task<BuyerWishlistStateDto> Create(WishlistNameRequest request, CancellationToken ct) => wishlists.NameAsync(null, request, ct);
    [HttpPut("{id:guid}")] public Task<BuyerWishlistStateDto> Rename(Guid id, WishlistNameRequest request, CancellationToken ct) => wishlists.NameAsync(id, request, ct);
    [HttpDelete("{id:guid}")] public Task<BuyerWishlistStateDto> Delete(Guid id, Guid revision, CancellationToken ct) => wishlists.DeleteAsync(id, revision, ct);
    [HttpPut("items/{listingId:guid}")] public Task<BuyerWishlistStateDto> Save(Guid listingId, WishlistMembershipRequest request, CancellationToken ct) => wishlists.SetMembershipAsync(listingId, request, ct);
    [HttpDelete("{id:guid}/unavailable")] public Task<BuyerWishlistStateDto> Clear(Guid id, Guid revision, CancellationToken ct) => wishlists.ClearUnavailableAsync(id, revision, ct);
}
