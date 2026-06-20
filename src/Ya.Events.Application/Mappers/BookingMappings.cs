using Ya.Events.Application.DTOs.Bookings;
using Ya.Events.Domain.Entities;

namespace Ya.Events.Application.Mappers;

public static class BookingMappings
{
    public static BookingResponse ToResponse(this Booking entity)
    {
        return new BookingResponse(
            entity.Id,
            entity.EventId,
            entity.Status,
            entity.CreatedAt,
            entity.ProcessedAt);
    }
}
