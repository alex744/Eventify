using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Services;

public class EventService : IEventService
{
    private readonly List<Event> _events;

    public EventService(IStore<Event> store)
    {
        _events = store.Collection;
    }

    public Task<PaginatedResult<Event>> GetAllAsync(
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

        var query = _events.AsEnumerable();

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

        int filteredCount = query.Count();

        var items = query
            .OrderByDescending(e => e.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new PaginatedResult<Event>(items, filteredCount, page, items.Count);
        return Task.FromResult(result);
    }

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var entity = _events.Find(e => e.Id == id);
        return Task.FromResult(entity);
    }

    public Task<Event> CreateAsync(Event entity, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _events.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<Event> UpdateAsync(Guid id, Event entity, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var existing = _events.Find(e => e.Id == id);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        existing.Title = entity.Title;
        existing.StartAt = entity.StartAt;
        existing.EndAt = entity.EndAt;
        existing.Description = entity.Description;

        return Task.FromResult(existing);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var existing = _events.Find(e => e.Id == id);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        _events.Remove(existing);
        return Task.CompletedTask;
    }
}
