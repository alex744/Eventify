using Ya.Users.Domain.Entities;

namespace Ya.Users.Application.Abstractions.Persistence.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByLoginAsync(string login, CancellationToken ct = default);
    Task<User> CreateAsync(User entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
