using Ya.Events.WebApi.Enums;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Services;

public class BookingService : IBookingService
{
    private readonly IBookingStore _bookingStorage;
    private readonly IEventService _eventService;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

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

        // Критическая секция – захватываем разделяемый семафор
        await _semaphore.WaitAsync(ct);
        try
        {
            // 1. Получаем событие
            var existingEvent = await _eventService.GetByIdAsync(eventId);
            if (existingEvent is null)
                throw new NotFoundException($"Событие с идентификатором '{eventId}' не найдено.");

            // 2. Атомарно проверяем и резервируем место
            if (!existingEvent.TryReserveSeats())
                throw new NoAvailableSeatsException("No available seats for this event");

            // 3. Сохраняем обновлённое событие (место занято)
            await _eventService.UpdateAsync(existingEvent.Id, existingEvent, ct);

            // 4. Создаём бронь
            var booking = new Booking(
                Guid.NewGuid(),
                eventId,
                BookingStatus.Pending,
                DateTime.UtcNow);

            // 5. Сохраняем бронь
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
