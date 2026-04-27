using System.Collections.Concurrent;
using Ya.Events.WebApi.Enums;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Stores;

public class InMemoryBookingStore : IBookingStore
{
    private readonly ConcurrentDictionary<Guid, Booking> _bookings = new();

    public Task AddAsync(Booking booking, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_bookings.TryAdd(booking.Id, booking))
            throw new InvalidOperationException($"Бронирование с идентификатором '{booking.Id}' уже существует.");

        return Task.CompletedTask;
    }

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _bookings.TryGetValue(id, out var booking);
        return Task.FromResult(booking);
    }

    public Task UpdateAsync(Booking booking, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_bookings.TryGetValue(booking.Id, out var existing))
            throw new NotFoundException($"Бронь {booking.Id} не найдена.");

        //if (existing.Status != BookingStatus.Pending)
        //    throw new BookingNotPendingException(existing);

        _bookings[booking.Id] = booking;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Booking>> GetAllPendingAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var pending = _bookings.Values
            .Where(b => b.Status == BookingStatus.Pending)
            .ToList();

        return Task.FromResult<IReadOnlyList<Booking>>(pending);
    }
}
