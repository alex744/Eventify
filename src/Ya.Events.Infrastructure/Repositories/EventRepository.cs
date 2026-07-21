using Microsoft.EntityFrameworkCore;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Application.DTOs;
using Ya.Events.Domain.Entities;
using Ya.Events.Infrastructure.Persistence;

namespace Ya.Events.Infrastructure.Repositories;

public class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var query = _context.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(e => e.Title.ToLower().Contains(title.ToLower()));
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.StartAt >= from);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.EndAt <= to);
        }

        int filteredCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Event>(items, filteredCount, page, pageSize);
    }

    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<Event> CreateAsync(Event entity, CancellationToken ct = default)
    {
        await _context.Events.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Event?> UpdateAsync(Guid id, Event entity, CancellationToken ct = default)
    {
        var existing = await _context.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (existing is null)
            return null;

        existing.Update(entity.Title, entity.StartAt, entity.EndAt, entity.Description);
        await _context.SaveChangesAsync(ct);

        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _context.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (existing is null)
            return false;

        _context.Events.Remove(existing);
        int rowsAffected = await _context.SaveChangesAsync(ct);
        return rowsAffected > 0;
    }
}
