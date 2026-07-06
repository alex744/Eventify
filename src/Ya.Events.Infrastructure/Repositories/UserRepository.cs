using Microsoft.EntityFrameworkCore;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Domain.Entities;
using Ya.Events.Infrastructure.Persistence;

namespace Ya.Events.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByLoginAsync(string login, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Login == login, ct);

    public async Task<User> CreateAsync(User entity, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
