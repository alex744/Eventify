using Microsoft.AspNetCore.Mvc;
using System.Net;
using Ya.Events.WebApi.DTOs.Requests;
using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Mappings;

namespace Ya.Events.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public ApiResult<EventResponse[]> GetAll()
    {
        return new ApiResult<EventResponse[]>
        {
            Data = _eventService.GetAll().Select(e => e.ToResponse()).ToArray(),
            Success = true,
            StatusCode = HttpStatusCode.OK,
            Message = "Получаем список всех событий"
        };
    }

    /// <summary>
    /// Получить событие по id
    /// GET /events/{id}
    /// </summary>    
    [HttpGet("{id}")]
    public ApiBaseResult GetById(Guid id)
    {
        try
        {
            return new ApiResult<EventResponse>
            {
                Data = _eventService.GetById(id).ToResponse(),
                Success = true,
                StatusCode = HttpStatusCode.OK,
                Message = "Получаем событие по ID"
            };
        }
        catch
        {
            return new ApiResult
            {
                Success = false,
                StatusCode = HttpStatusCode.NotFound,
                Message = $"Не удалось найти событие с ID: [{id}]"
            };
        }
    }

    /// <summary>
    /// Создать событие
    /// POST /events
    /// </summary>    
    [HttpPost]
    public ApiResult Create([FromBody] CreateEventRequest request)
    {
        var @event = _eventService.Create(
            request.Title,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.Description);

        return new ApiResult
        {
            Success = true,
            StatusCode = HttpStatusCode.Created,
            Message = "Событие успешно создано"
        };
    }

    /// <summary>
    /// Обновить событие целиком
    /// PUT /events/{id}
    /// </summary>    
    [HttpPut("{id}")]
    public ApiResult Update(Guid id, [FromBody] CreateEventRequest request)
    {
        try
        {
            _eventService.Update(
                id,
                request.Title,
                request.StartAt!.Value,
                request.EndAt!.Value,
                request.Description);

            return new ApiResult
            {
                Success = true,
                StatusCode = HttpStatusCode.NoContent,
                Message = "Событие успешно обновлено"
            };
        }
        catch
        {
            return new ApiResult
            {
                Success = false,
                StatusCode = HttpStatusCode.NotFound,
                Message = $"Не удалось найти событие с ID: [{id}]"
            };
        }
    }

    /// <summary>
    /// Удалить событие
    /// DELETE /events/{id}
    /// </summary>    
    [HttpDelete("{id}")]
    public ApiResult Delete(Guid id)
    {
        try
        {
            _eventService.Delete(id);

            return new ApiResult
            {
                Success = true,
                StatusCode = HttpStatusCode.NoContent,
                Message = "Событие успешно удалено"
            };
        }
        catch
        {
            return new ApiResult
            {
                Success = false,
                StatusCode = HttpStatusCode.NotFound,
                Message = $"Не удалось найти событие с ID: [{id}]"
            };
        }
    }
}
