using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Mappings;

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
