using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Interfaces;

public interface IEventService
{
    PaginatedResult<Event> GetAll(string? title, DateTime? from, DateTime? to, int page, int pageSize);
    Event? GetById(Guid id);
    Event Create(Event entity);
    Event Update(Guid id, Event entity);
    void Delete(Guid id);
}
