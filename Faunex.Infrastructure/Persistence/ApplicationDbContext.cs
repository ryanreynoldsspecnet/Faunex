using Microsoft.EntityFrameworkCore;
using Faunex.Domain.Entities;
using Faunex.Domain.Enums;
using Faunex.Application.Interfaces;
using Faunex.Application.Authorization;

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
    public DbSet<TenantDomain> TenantDomains { get; set; } = null!;
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
    public DbSet<ListingDocumentRequirement> ListingDocumentRequirements { get; set; } = null!;
    public DbSet<ListingFile> ListingFiles { get; set; } = null!;
    public DbSet<ListingReviewEvent> ListingReviewEvents { get; set; } = null!;

    private Guid? CurrentTenantId => _tenantContext.TenantId;

    private bool IsPlatformAdmin => _tenantContext.IsPlatformAdmin;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries<ListingReviewEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Review history is append-only.");
        var changes = ChangeTracker.Entries<ListingCompliance>()
            .Where(x => x.State == EntityState.Added || (x.State == EntityState.Modified
                && (x.Property(y => y.Status).IsModified || x.Property(y => y.LastUpdatedAt).IsModified)))
            .ToList();
        foreach (var change in changes)
        {
            if (change.Entity.Listing is null)
                await change.Reference(x => x.Listing).LoadAsync(cancellationToken);
            if (change.Entity.Listing is { } listing && Entry(listing).State != EntityState.Added)
                listing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        foreach (var entry in ChangeTracker.Entries<Listing>().Where(x => x.State == EntityState.Modified))
        {
            entry.Entity.Revision = Guid.NewGuid();
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
        foreach (var entry in ChangeTracker.Entries<Auction>().Where(x => x.State == EntityState.Modified))
        {
            entry.Entity.Revision = Guid.NewGuid();
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
        foreach (var change in changes)
        {
            var listing = change.Entity.Listing ?? throw new InvalidOperationException("Review history requires its listing.");
            var actorId = (_tenantContext as ITenantContextWithActor)?.ActorId;
            ListingReviewEvents.Add(new ListingReviewEvent
            {
                TenantId = listing.TenantId, ListingId = listing.Id, Listing = listing, Revision = listing.Revision,
                FromStatus = change.State == EntityState.Added ? null : change.OriginalValues.GetValue<ListingComplianceStatus>(nameof(ListingCompliance.Status)),
                ToStatus = change.Entity.Status, ActorId = actorId,
                ActorKind = actorId is null ? "System" : IsPlatformAdmin ? "Platform reviewer" : "Tenant user",
                Notes = change.Entity.Status is ListingComplianceStatus.Approved or ListingComplianceStatus.Rejected or ListingComplianceStatus.Suspended
                    ? change.Entity.ReviewNotes : null
            });
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Listing>().Property(x => x.Revision).IsConcurrencyToken();
        modelBuilder.Entity<Auction>().Property(x => x.Revision).IsConcurrencyToken();
        modelBuilder.Entity<Bid>().HasIndex(x => new { x.AuctionId, x.BidNumber }).IsUnique().HasFilter("\"BidNumber\" > 0");
        modelBuilder.Entity<Auction>().HasIndex(x => new { x.RulesVersion, x.State, x.EndsAt });
        modelBuilder.Entity<ListingReviewEvent>().HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ListingReviewEvent>().HasIndex(x => new { x.TenantId, x.ListingId, x.Id });
        modelBuilder.Entity<ListingReviewEvent>().HasIndex(x => new { x.ListingId, x.Revision }).IsUnique();
        modelBuilder.Entity<ListingReviewEvent>().Property(x => x.Notes).HasMaxLength(2000);
        modelBuilder.Entity<ListingReviewEvent>().HasQueryFilter(x => IsPlatformAdmin || (CurrentTenantId != null && x.TenantId == CurrentTenantId));
        modelBuilder.Entity<ListingFile>().HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ListingFile>().HasIndex(x => x.StorageKey).IsUnique();
        modelBuilder.Entity<ListingFile>().HasIndex(x => new { x.TenantId, x.ListingId });
        modelBuilder.Entity<ListingFile>().HasQueryFilter(x => IsPlatformAdmin || (CurrentTenantId != null && x.TenantId == CurrentTenantId));
        modelBuilder.Entity<ListingDocument>().HasOne(x => x.StoredFile).WithOne().HasForeignKey<ListingDocument>(x => x.StoredFileId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ListingDocumentRequirement>()
            .HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ListingDocumentRequirement>()
            .HasIndex(x => new { x.TenantId, x.AnimalClass, x.DocumentType }).IsUnique();

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

        modelBuilder.Entity<TenantDomain>()
            .HasOne(x => x.Tenant)
            .WithMany(x => x.Domains)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TenantDomain>()
            .HasIndex(x => x.Hostname)
            .IsUnique();

        modelBuilder.Entity<TenantDomain>()
            .Property(x => x.VerificationToken).IsConcurrencyToken();
        modelBuilder.Entity<TenantDomain>()
            .Property(x => x.VerifiedAt).IsConcurrencyToken();

        // Tenant scoping
        // Rules:
        // - Platform admin (TenantId == null) can query across tenants.
        // - Non-admin must have a TenantId; otherwise queries return empty.
        // TODO: Replace StubTenantContext with an auth-backed implementation.

        modelBuilder.Entity<Listing>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.TenantId == CurrentTenantId));

        modelBuilder.Entity<Auction>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.TenantId == CurrentTenantId));

        modelBuilder.Entity<Bid>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.TenantId == CurrentTenantId));

        modelBuilder.Entity<Document>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.TenantId == CurrentTenantId));

        modelBuilder.Entity<BirdDetails>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.Listing != null && x.Listing.TenantId == CurrentTenantId));

        modelBuilder.Entity<LivestockDetails>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.Listing != null && x.Listing.TenantId == CurrentTenantId));

        modelBuilder.Entity<GameAnimalDetails>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.Listing != null && x.Listing.TenantId == CurrentTenantId));

        modelBuilder.Entity<PoultryDetails>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.Listing != null && x.Listing.TenantId == CurrentTenantId));

        modelBuilder.Entity<ListingCompliance>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.TenantId == CurrentTenantId));

        modelBuilder.Entity<ListingDocument>().HasQueryFilter(x =>
            IsPlatformAdmin || (CurrentTenantId != null && x.TenantId == CurrentTenantId));

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
