using Ya.Events.Domain.Entities;

namespace Ya.Events.Application.Abstractions.Services;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken ct = default);
    Task<Booking?> GetBookingByIdAsync(Guid bookingId, CancellationToken ct = default);
    Task<Booking> CancelBookingAsync(Guid bookingId, Guid requesterUserId, bool isAdmin, CancellationToken ct = default);
}
