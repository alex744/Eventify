using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Mappings;

public static class EventMappings
{
    public static EventResponse ToResponse(this Event @event)
    {
        return new EventResponse(
            @event.Id,
            @event.Title,
            @event.Description,
            @event.StartAt,
            @event.EndAt);
    }
}
