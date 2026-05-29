using Microsoft.EntityFrameworkCore;
using Ya.Events.WebApi.DataAccess;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Services;

public class BookingService : IBookingService
{
    private readonly AppDbContext _context;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public BookingService(AppDbContext context)
    {
        _context = context;
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
            var existingEvent = await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
            if (existingEvent is null)
                throw new NotFoundException($"Событие с идентификатором '{eventId}' не найдено.");

            // 2. Атомарно проверяем и резервируем место
            if (!existingEvent.TryReserveSeats())
                throw new NoAvailableSeatsException("Свободных мест на это событие нет.");

            // 3. Создаём и сохраняем бронь
            var booking = Booking.CreatePending(eventId);
            await _context.Bookings.AddAsync(booking, ct);
            await _context.SaveChangesAsync(ct);

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
        => await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, ct);
}
