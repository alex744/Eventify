using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Interfaces;

public interface IBookingStore
{
    Task AddAsync(Booking booking, CancellationToken ct = default);
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(Booking booking, CancellationToken ct = default);
    Task<IReadOnlyList<Booking>> GetAllPendingAsync(CancellationToken ct = default);
}
