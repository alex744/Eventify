using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.DTOs.Bookings;
using Ya.Events.Application.DTOs.Events;
using Ya.Events.Application.DTOs.Responses;
using Ya.Events.Application.Mappers;
using Ya.Events.WebApi.Extensions;

namespace Ya.Events.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly IBookingService _bookingService;

    public EventsController(IEventService eventService, IBookingService bookingService)
    {
        _eventService = eventService;
        _bookingService = bookingService;
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
    /// Получить событие по id
    /// GET /events/{id}
    /// </summary>    
    [HttpGet("{id}")]
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
    [HttpPut("{id}")]
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
    [HttpDelete("{id}")]
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

    /// <summary>
    /// Создание брони для события.
    /// POST /events/{eventId}/book
    /// </summary>
    /// <param name="eventId">Идентификатор события.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Бронь со статусом Pending; код 202 Accepted с Location в заголовке.</returns>
    /// <response code="202">Бронь успешно создана и находится в ожидании подтверждения.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="404">Событие с указанным идентификатором не найдено.</response>
    /// <response code="409">Нет доступных мест для бронирования.</response>
    [Authorize]
    [HttpPost("{eventId}/book")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(BookingResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> CreateBookingAsync(Guid eventId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var booking = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        return AcceptedAtAction("GetBooking", "Bookings", new { id = booking.Id }, booking.ToResponse());
    }
}
