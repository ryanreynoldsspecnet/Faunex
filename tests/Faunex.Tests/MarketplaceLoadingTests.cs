using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Faunex.Api.Controllers;
using Faunex.Api.Tenancy;
using Faunex.Application.DTOs;
using Faunex.Application.Tenancy;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Faunex.Tests;

public sealed class MarketplaceLoadingTests
{
    [Theory]
    [InlineData("browse")]
    [InlineData("detail")]
    [InlineData("auction")]
    public void Approved_listing_composition_translates_on_production_provider(string operation)
    {
        using var db = new ApplicationDbContextFactory().CreateDbContext([]);
        var controller = new MarketplaceController(db, new TenantDomainResolver(db), TimeProvider.System);
        // Compile the production query without connecting to a database; custom projections
        // must remain composable for listing ordering, detail filters and auction subqueries.
        var query = (IQueryable)typeof(MarketplaceController)
            .GetMethod("QueryApprovedListings", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(controller, [Guid.NewGuid()])!;
        var row = Expression.Parameter(query.ElementType, "row");
        var listing = Expression.Property(row, "Listing");
        var key = Expression.Property(listing, "Id");
        var expression = operation switch
        {
            "browse" => Expression.Call(typeof(Queryable), "OrderByDescending", [query.ElementType, typeof(DateTimeOffset?)],
                query.Expression, Expression.Quote(Expression.Lambda(Expression.Property(row, "SortAt"), row))),
            "detail" => Expression.Call(typeof(Queryable), "Where", [query.ElementType], query.Expression,
                Expression.Quote(Expression.Lambda(Expression.Equal(key, Expression.Constant(Guid.NewGuid())), row))),
            _ => Expression.Call(typeof(Queryable), "Select", [query.ElementType, typeof(Guid)], query.Expression,
                Expression.Quote(Expression.Lambda(key, row)))
        };
        var sql = query.Provider.CreateQuery(expression).ToQueryString();
        Assert.Contains("SELECT", sql);
        if (operation == "browse") Assert.Contains("ORDER BY", sql);
    }

    [PostgresFact]
    public async Task Empty_verified_marketplace_returns_successful_empty_listing_collection()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var host = AccountApiFixture.TenantHost(await app.AddTenantAsync());
        var response = await app.Client.GetAsync($"/api/marketplace/listings?host={host}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<ListingDto>>())!);
    }

    [PostgresFact]
    public async Task Public_listings_are_approved_active_tenant_scoped_ordered_and_accessible_by_id()
    {
        await using var app = await AccountApiFixture.CreateAsync();
        var first = await app.AddTenantAsync();
        var second = await app.AddTenantAsync();
        var now = DateTimeOffset.UtcNow;
        using var scope = app.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seller = new User { Email = "seller@example.test", DisplayName = "Test seller", TenantId = first };
        db.Add(seller);
        var older = Listing(first, seller.Id, "Older approved", now.AddDays(-3));
        var newer = Listing(first, seller.Id, "Newer approved", now.AddDays(-2));
        var fallback = Listing(first, seller.Id, "Null review timestamp", now.AddDays(-1));
        var draft = Listing(first, seller.Id, "Draft", now);
        var inactive = Listing(first, seller.Id, "Inactive", now); inactive.IsActive = false;
        var other = Listing(second, seller.Id, "Other tenant", now);
        db.AddRange(older, newer, fallback, draft, inactive, other);
        db.AddRange(Compliance(older, now.AddDays(-3)), Compliance(newer, now), Compliance(fallback, null),
            Compliance(draft, null, ListingComplianceStatus.Draft), Compliance(inactive, now), Compliance(other, now));
        await db.SaveChangesAsync();
        var host = AccountApiFixture.TenantHost(first);
        app.Client.DefaultRequestHeaders.Remove(MarketplaceHosts.OriginHeader);
        app.Client.DefaultRequestHeaders.Add(MarketplaceHosts.OriginHeader, host);
        var response = await app.Client.GetAsync($"/api/marketplace/listings?host={host}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var listings = (await response.Content.ReadFromJsonAsync<List<ListingDto>>())!;
        Assert.Equal(new[] { newer.Id, fallback.Id, older.Id }, listings.Select(x => x.Id));
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync($"/api/marketplace/listings/{newer.Id}?host={host}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync($"/api/marketplace/listings/{newer.Id}/auctions?host={host}")).StatusCode);
        foreach (var hidden in new[] { draft.Id, inactive.Id, other.Id })
            Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync($"/api/marketplace/listings/{hidden}?host={host}")).StatusCode);
    }

    private static Listing Listing(Guid tenant, Guid seller, string title, DateTimeOffset created) =>
        new() { TenantId = tenant, SellerId = seller, Title = title, CreatedAt = created, CurrencyCode = "ZAR" };
    private static ListingCompliance Compliance(Listing listing, DateTimeOffset? reviewed, ListingComplianceStatus status = ListingComplianceStatus.Approved) =>
        new() { ListingId = listing.Id, TenantId = listing.TenantId, Status = status, ReviewedAt = reviewed };
}

internal sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FAUNEX_TEST_POSTGRES")))
            Skip = "Requires isolated PostgreSQL (DateTimeOffset ordering is unsupported by SQLite). Runs in CI.";
    }
}
