using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.DTOs;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.Exceptions;

namespace Ya.Events.Application.Services;

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
        // Валидация диапазона дат — бизнес-правило
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ArgumentException("Дата начала (from) не может быть позже даты окончания (to).");

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
        var existing = await _repository.UpdateAsync(id, entity, ct);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, ct);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        await _repository.DeleteAsync(id, ct);
    }
}
