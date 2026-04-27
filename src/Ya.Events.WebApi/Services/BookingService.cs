using Ya.Events.WebApi.Enums;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Services;

public class BookingService : IBookingService
{
    private readonly IBookingStore _bookingStorage;
    private readonly IEventService _eventService;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public BookingService(IBookingStore bookingStorage, IEventService eventService)
    {
        _bookingStorage = bookingStorage;
        _eventService = eventService;
    }

    /// <summary>
    /// Создаёт новую бронь для указанного события.
    /// </summary>
    /// <param name="eventId">Идентификатор события.</param>
    /// <returns>Созданная бронь.</returns>    
    public async Task<Booking> CreateBookingAsync(Guid eventId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var existingEvent = await _eventService.GetByIdAsync(eventId);
        if (existingEvent is null)
            throw new NotFoundException($"Событие с идентификатором '{eventId}' не найдено.");

        await _semaphore.WaitAsync(ct);
        try
        {
            if (!existingEvent.TryReserveSeats())
                throw new NoAvailableSeatsException("No available seats for this event");

            var booking = new Booking(
                Guid.NewGuid(),
                eventId,
                BookingStatus.Pending,
                DateTime.UtcNow);

            await _bookingStorage.AddAsync(booking, ct);
            return booking;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Возвращает бронь по её идентификатору.
    /// </summary>
    /// <param name="bookingId">Идентификатор брони.</param>
    /// <returns>Бронь, если найдена; иначе null.</returns>    
    public async Task<Booking?> GetBookingByIdAsync(Guid bookingId, CancellationToken ct = default)
        => await _bookingStorage.GetByIdAsync(bookingId, ct);
}
