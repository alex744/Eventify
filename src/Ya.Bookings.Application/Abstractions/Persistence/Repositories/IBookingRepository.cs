using Ya.Bookings.Domain.Entities;

namespace Ya.Bookings.Application.Abstractions.Persistence.Repositories;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Booking> CreateAsync(Booking entity, CancellationToken ct = default);
    //Task<Event?> GetEventByIdAsync(Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken ct = default);
    Task<int> CountActiveBookingsAsync(Guid userId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
