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
    //[Range(nameof(pageSize),1,100,"")]
    public ActionResult<PaginatedResult<EventResponse>> GetAll(
        [FromQuery] string? title = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10)
    {
        // Получение данных из сервиса
        var paginatedResult = _eventService.GetAll(title, from, to, page, pageSize);

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
    public ActionResult<EventResponse> GetById(Guid id)
    {
        var entity = _eventService.GetById(id);
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
    public ActionResult<EventResponse> Create([FromBody] CreateEventRequest request)
    {
        var created = _eventService.Create(request.ToEvent());
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponse());
    }

    /// <summary>
    /// Обновить событие целиком
    /// PUT /events/{id}
    /// </summary>    
    [HttpPut("{id}")]
    public ActionResult<EventResponse> Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var updated = _eventService.Update(id, request.ToEvent());
        return updated.ToResponse();
    }

    /// <summary>
    /// Удалить событие
    /// DELETE /events/{id}
    /// </summary>    
    [HttpDelete("{id}")]
    public IActionResult Delete(Guid id)
    {
        _eventService.Delete(id);
        return NoContent();
    }
}
