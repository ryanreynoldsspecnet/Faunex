using Microsoft.EntityFrameworkCore;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Faunex.Application.Interfaces;

namespace Faunex.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<BirdSpecies> BirdSpeciesSet { get; set; } = null!;

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<Listing> Listings { get; set; } = null!;
    public DbSet<Auction> Auctions { get; set; } = null!;
    public DbSet<Bid> Bids { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;

    public DbSet<BirdDetails> BirdDetails { get; set; } = null!;
    public DbSet<LivestockDetails> LivestockDetails { get; set; } = null!;
    public DbSet<GameAnimalDetails> GameAnimalDetails { get; set; } = null!;
    public DbSet<PoultryDetails> PoultryDetails { get; set; } = null!;

    public DbSet<ListingCompliance> ListingCompliances { get; set; } = null!;
    public DbSet<ListingDocument> ListingDocuments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BirdDetails>()
            .HasKey(x => x.ListingId);

        modelBuilder.Entity<LivestockDetails>()
            .HasKey(x => x.ListingId);

        modelBuilder.Entity<GameAnimalDetails>()
            .HasKey(x => x.ListingId);

        modelBuilder.Entity<PoultryDetails>()
            .HasKey(x => x.ListingId);

        modelBuilder.Entity<Listing>()
            .HasOne(x => x.BirdDetails)
            .WithOne(x => x.Listing)
            .HasForeignKey<BirdDetails>(x => x.ListingId);

        modelBuilder.Entity<Listing>()
            .HasOne(x => x.LivestockDetails)
            .WithOne(x => x.Listing)
            .HasForeignKey<LivestockDetails>(x => x.ListingId);

        modelBuilder.Entity<Listing>()
            .HasOne(x => x.GameAnimalDetails)
            .WithOne(x => x.Listing)
            .HasForeignKey<GameAnimalDetails>(x => x.ListingId);

        modelBuilder.Entity<Listing>()
            .HasOne(x => x.PoultryDetails)
            .WithOne(x => x.Listing)
            .HasForeignKey<PoultryDetails>(x => x.ListingId);

        modelBuilder.Entity<ListingCompliance>()
            .HasKey(x => x.ListingId);

        modelBuilder.Entity<ListingCompliance>()
            .HasOne(x => x.Listing)
            .WithOne(x => x.Compliance)
            .HasForeignKey<ListingCompliance>(x => x.ListingId);

        modelBuilder.Entity<ListingDocument>()
            .HasOne(x => x.Listing)
            .WithMany(x => x.ComplianceDocuments)
            .HasForeignKey(x => x.ListingId);

        // Tenant scoping
        // Rules:
        // - Platform admin (TenantId == null) can query across tenants.
        // - Non-admin must have a TenantId; otherwise queries return empty.
        // TODO: Replace StubTenantContext with an auth-backed implementation.
        modelBuilder.Entity<Listing>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.TenantId == _tenantContext.TenantId.Value);

        modelBuilder.Entity<Auction>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.TenantId == _tenantContext.TenantId.Value);

        modelBuilder.Entity<Bid>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.TenantId == _tenantContext.TenantId.Value);

        modelBuilder.Entity<Document>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.TenantId == _tenantContext.TenantId.Value);

        modelBuilder.Entity<BirdDetails>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.Listing != null && x.Listing.TenantId == _tenantContext.TenantId.Value);

        modelBuilder.Entity<LivestockDetails>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.Listing != null && x.Listing.TenantId == _tenantContext.TenantId.Value);

        modelBuilder.Entity<GameAnimalDetails>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.Listing != null && x.Listing.TenantId == _tenantContext.TenantId.Value);

        modelBuilder.Entity<PoultryDetails>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.Listing != null && x.Listing.TenantId == _tenantContext.TenantId.Value);

        modelBuilder.Entity<ListingCompliance>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.TenantId == _tenantContext.TenantId.Value);

        modelBuilder.Entity<ListingDocument>().HasQueryFilter(x =>
            _tenantContext.IsPlatformAdmin
                ? true
                : _tenantContext.TenantId.HasValue && x.TenantId == _tenantContext.TenantId.Value);

        var seededAt = new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<BirdSpecies>().HasData(
            new BirdSpecies
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ScientificName = "Ara macao",
                CommonName = "Scarlet Macaw",
                CitesAppendix = CITESAppendix.AppendixI,
                IsEndangered = true,
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            },
            new BirdSpecies
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ScientificName = "Psittacus erithacus",
                CommonName = "African Grey Parrot",
                CitesAppendix = CITESAppendix.AppendixI,
                IsEndangered = true,
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            },
            new BirdSpecies
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ScientificName = "Falco peregrinus",
                CommonName = "Peregrine Falcon",
                CitesAppendix = CITESAppendix.AppendixI,
                IsEndangered = false,
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            },
            new BirdSpecies
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                ScientificName = "Cacatua galerita",
                CommonName = "Sulphur-crested Cockatoo",
                CitesAppendix = CITESAppendix.AppendixII,
                IsEndangered = false,
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            },
            new BirdSpecies
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                ScientificName = "Amazona aestiva",
                CommonName = "Blue-fronted Amazon",
                CitesAppendix = CITESAppendix.AppendixII,
                IsEndangered = false,
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            },
            new BirdSpecies
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                ScientificName = "Sturnus vulgaris",
                CommonName = "European Starling",
                CitesAppendix = CITESAppendix.NotListed,
                IsEndangered = false,
                CreatedAt = seededAt,
                UpdatedAt = seededAt
            }
        );
    }
}
