using Microsoft.EntityFrameworkCore;
using StormBird.Domain.Entities;
using StormBird.Infrastructure.Persistence;

namespace StormBird.Infrastructure.Repositories;

public sealed class UserRepository(ApplicationDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

    public Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Update(entity);
        return Task.CompletedTask;
    }
}
