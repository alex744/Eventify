using Ya.Events.Application.DTOs.Events;
using Ya.Events.Domain.Entities;

namespace Ya.Events.Application.Mappers;

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
        return Event.Create(
            request.Title,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.TotalSeats!.Value,
            request.Description
        );
    }

    public static Event ToEvent(this UpdateEventRequest request)
    {
        return Event.Create(
            request.Title,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.TotalSeats!.Value,
            request.Description
        );
    }
}
