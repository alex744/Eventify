using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.Exceptions;
using Ya.Events.Domain.ValueObjects;

namespace Ya.Events.Application.Services;

public class BookingService : IBookingService
{
    private const int MaxActiveBookingsPerUser = 10;
    private readonly IBookingRepository _repository;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public BookingService(IBookingRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Создаёт новую бронь для указанного события.
    /// </summary>
    /// <param name="eventId">Идентификатор события.</param>
    /// <param name="userId">Идентификатор пользователя, создающего бронь.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Созданная бронь.</returns>    
    public async Task<Booking> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Критическая секция – захватываем разделяемый семафор
        await _semaphore.WaitAsync(ct);
        try
        {
            // 1. Получаем событие
            var existingEvent = await _repository.GetEventByIdAsync(eventId, ct);
            if (existingEvent is null)
                throw new NotFoundException($"Событие с идентификатором '{eventId}' не найдено.");

            // 2. Запрет бронирования события, которое уже началось
            if (existingEvent.StartAt <= DateTime.UtcNow)
                throw new PastEventBookingException("Нельзя создать бронь на событие, которое уже началось.");

            // 3. Ограничение активных бронирований у пользователя
            var activeCount = await _repository.CountActiveBookingsAsync(userId, ct);
            if (activeCount >= MaxActiveBookingsPerUser)
                throw new TooManyActiveBookingsException($"У пользователя не может быть более {MaxActiveBookingsPerUser} активных броней.");

            // 4. Атомарно проверяем и резервируем место
            if (!existingEvent.TryReserveSeats())
                throw new NoAvailableSeatsException("Свободных мест на это событие нет.");

            // 5. Создаём и сохраняем бронь
            var booking = Booking.CreatePending(eventId, userId);
            await _repository.CreateAsync(booking, ct);

            // 6. Сохраняем изменения события в базе данных
            await _repository.SaveChangesAsync(ct);

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
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Бронь, если найдена; иначе null.</returns>    
    public async Task<Booking?> GetBookingByIdAsync(Guid bookingId, CancellationToken ct = default)
        => await _repository.GetByIdAsync(bookingId, ct);

    /// <summary>
    /// Возвращает бронь по её идентификатору с проверкой принадлежности пользователю.
    /// </summary>
    /// <param name="bookingId">Идентификатор брони.</param>
    /// <param name="userId">Идентификатор пользователя, запрашивающего бронь.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Бронь, если она принадлежит пользователю; иначе null.</returns>    
    public async Task<Booking?> GetBookingByIdAsync(Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await _repository.GetByIdAsync(bookingId, ct);
        return booking?.UserId == userId ? booking : null;
    }

    /// <summary>
    /// Отменяет бронь с проверкой прав.    
    /// </summary>
    /// <param name="bookingId">Идентификатор брони.</param>
    /// <param name="requesterUserId">Идентификатор пользователя, пытающегося отменить бронь.</param>
    /// <param name="isAdmin">Указывает, является ли пользователь администратором.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Отмененная бронь.</returns>
    public async Task<Booking> CancelBookingAsync(Guid bookingId, Guid requesterUserId, bool isAdmin, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Критическая секция – захватываем разделяемый семафор
        await _semaphore.WaitAsync(ct);
        try
        {
            // 1. Получаем бронь
            var booking = await _repository.GetByIdAsync(bookingId, ct);
            if (booking is null)
                throw new NotFoundException($"Бронь с идентификатором '{bookingId}' не найдена.");

            // 2. Проверяем права на отмену
            if (!isAdmin && booking.UserId != requesterUserId)
                throw new ForbiddenException("Недостаточно прав для отмены бронирования.");

            // 3. Если уже отменена — вернуть без изменений (идемпотентно)
            if (booking.Status == BookingStatus.Cancelled)
                return booking;

            // 4.Отмена брони
            booking.Cancel();

            // 5.Освобождаем место в событии, если событие существует
            var existingEvent = await _repository.GetEventByIdAsync(booking.EventId, ct);
            existingEvent?.ReleaseSeats();

            // 6. Сохраняем изменения в базе данных
            await _repository.SaveChangesAsync(ct);

            return booking;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
