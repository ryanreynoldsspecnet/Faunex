using Faunex.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faunex.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Listing> Listings { get; }
    DbSet<Auction> Auctions { get; }
    DbSet<Bid> Bids { get; }
    DbSet<Document> Documents { get; }

    DbSet<BirdDetails> BirdDetails { get; }
    DbSet<LivestockDetails> LivestockDetails { get; }
    DbSet<GameAnimalDetails> GameAnimalDetails { get; }
    DbSet<PoultryDetails> PoultryDetails { get; }

    DbSet<ListingCompliance> ListingCompliances { get; }
    DbSet<ListingDocument> ListingDocuments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
