using Microsoft.EntityFrameworkCore;
using StormBird.Domain.Entities;

namespace StormBird.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<BirdSpecies> BirdSpecies => Set<BirdSpecies>();
    public DbSet<BirdListing> BirdListings => Set<BirdListing>();
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<Bid> Bids => Set<Bid>();
}
