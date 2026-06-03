using Faunex.Application.Services;
using Faunex.Domain.Entities;
using Faunex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Tests;

public sealed class ListingBrowseServiceTests
{
    [Fact]
    public async Task BrowseApprovedListingsAsync_returns_only_active_approved_listings()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(new StubTenantContext
        {
            TenantId = tenantId
        });

        var approvedActive = CreateListing(tenantId, "Approved active");
        var approvedInactive = CreateListing(tenantId, "Approved inactive", isActive: false);
        var pendingActive = CreateListing(tenantId, "Pending active");

        db.Listings.AddRange(approvedActive, approvedInactive, pendingActive);
        db.ListingCompliances.AddRange(
            CreateCompliance(approvedActive, ListingComplianceStatus.Approved),
            CreateCompliance(approvedInactive, ListingComplianceStatus.Approved),
            CreateCompliance(pendingActive, ListingComplianceStatus.UnderReview));
        await db.SaveChangesAsync();

        var service = new ListingBrowseService(db);

        var result = await service.BrowseApprovedListingsAsync(CancellationToken.None);

        var listing = Assert.Single(result);
        Assert.Equal(approvedActive.Id, listing.Id);
        Assert.Equal("Approved active", listing.Title);
    }

    [Fact]
    public async Task BrowseApprovedListingsAsync_applies_tenant_scope_for_non_platform_users()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateDbContext(new StubTenantContext
        {
            TenantId = tenantId
        });

        var ownListing = CreateListing(tenantId, "Own tenant");
        var otherListing = CreateListing(otherTenantId, "Other tenant");

        db.Listings.AddRange(ownListing, otherListing);
        db.ListingCompliances.AddRange(
            CreateCompliance(ownListing, ListingComplianceStatus.Approved),
            CreateCompliance(otherListing, ListingComplianceStatus.Approved));
        await db.SaveChangesAsync();

        var service = new ListingBrowseService(db);

        var result = await service.BrowseApprovedListingsAsync(CancellationToken.None);

        var listing = Assert.Single(result);
        Assert.Equal(ownListing.Id, listing.Id);
    }

    [Fact]
    public async Task BrowseApprovedListingsAsync_allows_platform_admin_to_browse_across_tenants()
    {
        await using var db = CreateDbContext(new StubTenantContext
        {
            IsPlatformAdmin = true
        });

        var firstListing = CreateListing(Guid.NewGuid(), "First tenant");
        var secondListing = CreateListing(Guid.NewGuid(), "Second tenant");

        db.Listings.AddRange(firstListing, secondListing);
        db.ListingCompliances.AddRange(
            CreateCompliance(firstListing, ListingComplianceStatus.Approved),
            CreateCompliance(secondListing, ListingComplianceStatus.Approved));
        await db.SaveChangesAsync();

        var service = new ListingBrowseService(db);

        var result = await service.BrowseApprovedListingsAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Id == firstListing.Id);
        Assert.Contains(result, x => x.Id == secondListing.Id);
    }

    [Fact]
    public async Task GetApprovedListingByIdAsync_returns_null_when_listing_is_not_approved()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(new StubTenantContext
        {
            TenantId = tenantId
        });

        var listing = CreateListing(tenantId, "Pending listing");

        db.Listings.Add(listing);
        db.ListingCompliances.Add(CreateCompliance(listing, ListingComplianceStatus.UnderReview));
        await db.SaveChangesAsync();

        var service = new ListingBrowseService(db);

        var result = await service.GetApprovedListingByIdAsync(listing.Id, CancellationToken.None);

        Assert.Null(result);
    }

    private static ApplicationDbContext CreateDbContext(StubTenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    private static Listing CreateListing(Guid tenantId, string title, bool isActive = true)
    {
        return new Listing
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SellerId = Guid.NewGuid(),
            Title = title,
            StartingPrice = 100,
            CurrencyCode = "ZAR",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ListingCompliance CreateCompliance(Listing listing, ListingComplianceStatus status)
    {
        return new ListingCompliance
        {
            ListingId = listing.Id,
            TenantId = listing.TenantId,
            Status = status,
            ReviewedAt = status == ListingComplianceStatus.Approved ? DateTimeOffset.UtcNow : null,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
