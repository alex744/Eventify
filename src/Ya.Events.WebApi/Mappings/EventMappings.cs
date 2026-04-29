using Ya.Events.WebApi.DTOs.Requests;
using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Mappings;

public static class EventMappings
{
    public static EventResponse ToResponse(this Event entity)
    {
        return new EventResponse(
            entity.Id,
            entity.Title,
            entity.Description,
            entity.StartAt,
            entity.EndAt,
            entity.TotalSeats,
            entity.AvailableSeats);
    }

    public static Event ToEvent(this CreateEventRequest request)
    {
        return new Event(
            request.Title,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.TotalSeats!.Value,
            request.Description
        );
    }

    public static Event ToEvent(this UpdateEventRequest request)
    {
        return new Event(
            request.Title,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.TotalSeats!.Value,
            request.Description
        );
    }
}
