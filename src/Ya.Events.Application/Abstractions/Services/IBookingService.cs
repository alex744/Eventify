using Ya.Events.Domain.Entities;

namespace Ya.Events.Application.Abstractions.Services;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId, CancellationToken ct = default);
    Task<Booking?> GetBookingByIdAsync(Guid bookingId, CancellationToken ct = default);
}
