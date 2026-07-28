using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.DTOs;
using Ya.Events.Application.Mappers;

namespace Ya.Events.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    /// <summary>
    /// Получить список всех событий
    /// GET /events
    /// </summary>
    /// <param name="title">Поиск по названию</param>
    /// <param name="from">События, которые начинаются не раньше указанной даты</param>
    /// <param name="to">События, которые заканчиваются не позже указанной даты</param>    
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaginatedResult<EventResponse>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<PaginatedResult<EventResponse>>> GetAllAsync(
        [FromQuery] string? title = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        CancellationToken ct = default)
    {
        // Получение данных из сервиса
        var paginatedResult = await _eventService.GetAllAsync(title, from, to, page, pageSize, ct);

        // Маппинг доменных объектов в DTO ответа
        var items = paginatedResult.Items
            .Select(e => e.ToResponse())
            .ToList();

        // Формирование ответа с использованием данных из сервиса
        return new PaginatedResult<EventResponse>(
            items,
            paginatedResult.TotalCount,
            paginatedResult.CurrentPage,
            paginatedResult.PageSize);
    }

    /// <summary>
    /// Топ-10 самых популярных событий
    /// GET /events/top
    /// </summary>    
    [HttpGet("top")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<EventResponse>))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<IReadOnlyList<EventResponse>>> GetTopEventsAsync(CancellationToken ct = default)
    {
        var topEvents = await _eventService.GetTopEventsAsync(ct);
        var response = topEvents.Select(e => e.ToResponse()).ToList();
        return response;
    }

    /// <summary>
    /// Получить событие по id
    /// GET /events/{id}
    /// </summary>    
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EventResponse))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<EventResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _eventService.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Detail = $"Событие с идентификатором '{id}' не найдено."
            });
        }

        return entity.ToResponse();
    }

    /// <summary>
    /// Создать событие
    /// POST /events
    /// </summary>    
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(EventResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> CreateAsync([FromBody] CreateEventRequest request, CancellationToken ct = default)
    {
        var created = await _eventService.CreateAsync(request.ToEvent(), ct);
        return CreatedAtAction("GetById", new { id = created.Id }, created.ToResponse());
    }

    /// <summary>
    /// Обновить событие целиком
    /// PUT /events/{id}
    /// </summary>    
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(EventResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<EventResponse>> UpdateAsync(Guid id, [FromBody] UpdateEventRequest request, CancellationToken ct = default)
    {
        var updated = await _eventService.UpdateAsync(id, request.ToEvent(), ct);
        return updated.ToResponse();
    }

    /// <summary>
    /// Удалить событие
    /// DELETE /events/{id}
    /// </summary>    
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _eventService.DeleteAsync(id, ct);
        return NoContent();
    }
}
