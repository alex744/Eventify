using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Services;

public class EventService : IEventService
{
    private readonly List<Event> _events;

    public EventService(IStorage<Event> storage)
    {
        _events = storage.Collection;
    }

    public PaginatedResult<Event> GetAll(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10)
    {
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

        return new PaginatedResult<Event>(items, filteredCount, page, items.Count);
    }

    public Event? GetById(Guid id) => _events.Find(e => e.Id == id);

    public Event Create(Event entity)
    {
        _events.Add(entity);
        return entity;
    }

    public Event Update(Guid id, Event entity)
    {
        var existing = GetById(id);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        existing.Title = entity.Title;
        existing.StartAt = entity.StartAt;
        existing.EndAt = entity.EndAt;
        existing.Description = entity.Description;

        return existing;
    }

    public void Delete(Guid id)
    {
        var existing = GetById(id);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        _events.Remove(existing);
    }
}
