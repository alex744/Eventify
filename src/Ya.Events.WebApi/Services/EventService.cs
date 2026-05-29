using Microsoft.EntityFrameworkCore;
using Ya.Events.WebApi.DataAccess;
using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
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

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ArgumentException("Дата начала (from) не может быть позже даты окончания (to).");
        }

        var query = _context.Events.AsQueryable();

        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(e => e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
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
        var entity = await _context.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        return entity;
    }

    public async Task<Event> CreateAsync(Event entity, CancellationToken ct = default)
    {
        await _context.Events.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Event> UpdateAsync(Guid id, Event entity, CancellationToken ct = default)
    {
        var existing = await _context.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        existing.Title = entity.Title;
        existing.StartAt = entity.StartAt;
        existing.EndAt = entity.EndAt;
        existing.TotalSeats = entity.TotalSeats;
        existing.Description = entity.Description;

        await _context.SaveChangesAsync(ct);
        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _context.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        _context.Events.Remove(existing);
        await _context.SaveChangesAsync(ct);
    }
}
