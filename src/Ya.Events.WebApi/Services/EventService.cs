using System.Collections.ObjectModel;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Services;

public class EventService : IEventService
{
    private static readonly List<Event> _events = [];

    public ReadOnlyCollection<Event> GetAll()
    {
        return _events.AsReadOnly();
    }

    public Event GetById(Guid id)
    {
        var existing = _events.Find(e => e.Id == id);
        if (existing is null)
            throw new InvalidOperationException($"Событие с идентификатором {id} не найдено.");

        return existing;
    }

    public Event Create(string title, DateTime startAt, DateTime endAt, string? description = null)
    {
        var newEvent = new Event(title, startAt, endAt, description);
        _events.Add(newEvent);

        return newEvent;
    }

    public void Update(Guid id, string title, DateTime startAt, DateTime endAt, string? description = null)
    {
        var existing = _events.Find(e => e.Id == id);
        if (existing is null)
            throw new InvalidOperationException($"Событие с идентификатором {id} не найдено.");

        existing.Title = title;
        existing.StartAt = startAt;
        existing.EndAt = endAt;
        existing.Description = description;
    }

    public void Delete(Guid id)
    {
        var existing = _events.Find(e => e.Id == id);
        if (existing is null)
            throw new InvalidOperationException($"Событие с идентификатором {id} не найдено.");

        _events.Remove(existing);
    }
}
