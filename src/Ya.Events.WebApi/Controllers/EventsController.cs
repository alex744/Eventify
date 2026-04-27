using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Ya.Events.WebApi.DTOs.Requests;
using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Mappings;

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
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateEventRequest request, CancellationToken ct = default)
    {
        var created = await _eventService.CreateAsync(request.ToEvent(), ct);
        return CreatedAtAction("GetById", new { id = created.Id }, created.ToResponse());
    }

    /// <summary>
    /// Обновить событие целиком
    /// PUT /events/{id}
    /// </summary>    
    [HttpPut("{id}")]
    public async Task<ActionResult<EventResponse>> UpdateAsync(Guid id, [FromBody] UpdateEventRequest request, CancellationToken ct = default)
    {
        var updated = await _eventService.UpdateAsync(id, request.ToEvent(), ct);
        return updated.ToResponse();
    }

    /// <summary>
    /// Удалить событие
    /// DELETE /events/{id}
    /// </summary>    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _eventService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Создание брони
    /// POST /events/{id}/book
    /// </summary>    
    [HttpPost("{eventId}/book")]
    public async Task<IActionResult> CreateBookingAsync(Guid eventId, CancellationToken ct = default)
    {
        var booking = await _bookingService.CreateBookingAsync(eventId, ct);
        return AcceptedAtAction("GetBooking", "Bookings", new { id = booking.Id }, booking.ToResponse());
    }
}
