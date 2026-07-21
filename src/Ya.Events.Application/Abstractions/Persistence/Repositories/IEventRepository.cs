using Ya.Events.Application.DTOs;
using Ya.Events.Domain.Entities;

namespace Ya.Events.Application.Abstractions.Persistence.Repositories;

public interface IEventRepository
{
    Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Event> CreateAsync(Event entity, CancellationToken ct = default);
    Task<Event?> UpdateAsync(Guid id, Event entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
