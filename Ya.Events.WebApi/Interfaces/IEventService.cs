using System.Collections.ObjectModel;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Interfaces;

public interface IEventService
{
    ReadOnlyCollection<Event> GetAll();
    Event GetById(Guid id);
    Event Create(string title, DateTime startAt, DateTime endAt, string? description = null);
    void Update(Guid id, string title, DateTime startAt, DateTime endAt, string? description = null);
    void Delete(Guid id);
}
