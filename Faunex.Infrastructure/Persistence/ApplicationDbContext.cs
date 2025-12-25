using Microsoft.EntityFrameworkCore;
using StormBird.Domain.Entities;
using StormBird.Domain.Enums;

namespace StormBird.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<BirdSpecies> BirdSpeciesSet { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
