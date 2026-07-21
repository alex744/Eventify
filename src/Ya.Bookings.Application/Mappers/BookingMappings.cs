using Ya.Bookings.Application.DTOs;
using Ya.Bookings.Domain.Entities;

namespace Ya.Bookings.Application.Mappers;

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
