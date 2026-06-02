using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;
using Ya.Events.WebApi.Repositories;

namespace Ya.Events.WebApi.Services;

public class EventService : IEventService
{
    private readonly IEventRepository _repository;

    public EventService(IEventRepository repository)
    {
        _repository = repository;
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        return await _repository.GetAllAsync(title, from, to, page, pageSize, ct);
    }

    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _repository.GetByIdAsync(id, ct);
    }

    public async Task<Event> CreateAsync(Event entity, CancellationToken ct = default)
    {
        return await _repository.CreateAsync(entity, ct);
    }

    public async Task<Event> UpdateAsync(Guid id, Event entity, CancellationToken ct = default)
    {
        return await _repository.UpdateAsync(id, entity, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
    }
}
