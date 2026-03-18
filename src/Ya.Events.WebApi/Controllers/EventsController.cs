using Microsoft.AspNetCore.Mvc;
using System.Net;
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
    [HttpGet]
    public ActionResult<EventResponse[]> GetAll()
    {
        return _eventService.GetAll()
            .Select(e => e.ToResponse())
            .ToArray();
    }

    /// <summary>
    /// Получить событие по id
    /// GET /events/{id}
    /// </summary>    
    [HttpGet("{id}")]
    public ActionResult<EventResponse> GetById(Guid id)
    {
        try
        {
            return _eventService.GetById(id).ToResponse();
        }
        catch
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Создать событие
    /// POST /events
    /// </summary>    
    [HttpPost]
    public IActionResult Create([FromBody] CreateEventRequest request)
    {
        var @event = _eventService.Create(
            request.Title,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.Description);

        return CreatedAtAction(nameof(GetById), new { @event.Id }, @event);
    }

    /// <summary>
    /// Обновить событие целиком
    /// PUT /events/{id}
    /// </summary>    
    [HttpPut("{id}")]
    public IActionResult Update(Guid id, [FromBody] CreateEventRequest request)
    {
        try
        {
            _eventService.Update(
                id,
                request.Title,
                request.StartAt!.Value,
                request.EndAt!.Value,
                request.Description);

            return NoContent();
        }
        catch
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Удалить событие
    /// DELETE /events/{id}
    /// </summary>    
    [HttpDelete("{id}")]
    public IActionResult Delete(Guid id)
    {
        try
        {
            _eventService.Delete(id);
            return NoContent();
        }
        catch
        {
            return NotFound();
        }
    }
}
