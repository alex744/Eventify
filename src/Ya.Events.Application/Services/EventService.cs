using Ya.Events.Application.Abstractions.Caching;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.Constants;
using Ya.Events.Application.DTOs;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.Exceptions;

namespace Ya.Events.Application.Services;

public class EventService : IEventService
{
    private readonly ICache _cache;
    private readonly IEventRepository _repository;
    private readonly TimeSpan _eventByIdTtl;
    private readonly TimeSpan _topEventsTtl;

    public EventService(
        ICache cache,
        IEventRepository repository,
        ICacheTtlProvider cacheTtlProvider)
    {
        _cache = cache;
        _repository = repository;
        _eventByIdTtl = cacheTtlProvider.EventByIdTtl;
        _topEventsTtl = cacheTtlProvider.TopEventsTtl;
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
        var cacheKey = CacheKeys.Event(id);

        // Шаг 1: смотрим в кеш
        var cached = await _cache.GetAsync<Event>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        // Шаг 2: идём в базу
        var @event = await _repository.GetByIdAsync(id, ct);
        if (@event is null) return null;

        // Шаг 3: кладём в кеш
        await _cache.SetAsync(cacheKey, @event, _eventByIdTtl, ct);

        return @event;
    }

    public async Task<IReadOnlyList<Event>> GetTopEventsAsync(CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.Top10Events;

        // Шаг 1: смотрим в кеш
        var cached = await _cache.GetAsync<IReadOnlyList<Event>>(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        // Шаг 2: идём в базу
        var events = await _repository.GetTop10Async(ct);

        // Шаг 3: кладём в кеш
        await _cache.SetAsync(cacheKey, events, _topEventsTtl, ct);

        return events;
    }

    public async Task<Event> CreateAsync(Event entity, CancellationToken ct = default)
    {
        return await _repository.CreateAsync(entity, ct);
    }

    public async Task<Event> UpdateAsync(Guid id, Event entity, CancellationToken ct = default)
    {
        // Шаг 1. Обновляем данные в основном источнике
        var existing = await _repository.UpdateAsync(id, entity, ct);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        // Шаг 2. Активная инвалидация (удаление из кеша)
        var cacheKey = CacheKeys.Event(id);
        await _cache.RemoveAsync(cacheKey, ct);

        return existing;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Шаг 1. Проверяем существование события
        var existing = await _repository.GetByIdAsync(id, ct);
        if (existing is null)
            throw new NotFoundException($"Событие с идентификатором '{id}' не найдено.");

        // Шаг 2. Удаляем событие из основного источника
        await _repository.DeleteAsync(id, ct);

        // Шаг 3. Активная инвалидация (удаление из кеша)
        var cacheKey = CacheKeys.Event(id);
        await _cache.RemoveAsync(cacheKey, ct);
    }
}
