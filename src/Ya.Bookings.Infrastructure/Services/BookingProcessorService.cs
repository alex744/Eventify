using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ya.Bookings.Application.Abstractions.Persistence.Repositories;
using Ya.Bookings.Application.Abstractions.Services;
using Ya.Bookings.Domain.ValueObjects;
using Ya.Shared.Contracts.Events;

namespace Ya.Bookings.Infrastructure.Services;

public class BookingProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessorService> _logger;

    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _processingDelay = TimeSpan.FromSeconds(2);

    public BookingProcessorService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingProcessorService запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IReadOnlyList<Guid> pendingBookingIds;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                    pendingBookingIds = await bookingRepository.GetPendingBookingIdsAsync(stoppingToken);
                }

                var tasks = pendingBookingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка в основном цикле процессора.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("BookingProcessorService остановлен.");
    }

    /// <summary>
    /// Обрабатывает одну бронь: имитирует внешний вызов, затем
    /// проверяет существование события и подтверждает или отклоняет бронь.
    /// </summary>
    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Обработка брони '{Id}'...", bookingId);

            // 1. Имитация внешнего запроса (выполняется параллельно для разных броней)
            await Task.Delay(_processingDelay, stoppingToken);

            // 2. Получаем сервисы в новом scope
            using var scope = _scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var bookingEventPublisher = scope.ServiceProvider.GetRequiredService<IBookingEventPublisher>();

            // 3. Получаем бронь и проверяем её статус
            var booking = await bookingRepository.GetByIdAsync(bookingId, stoppingToken);
            if (booking == null || booking.Status != BookingStatus.Pending)
                return;

            // 4. Проверяем, существует ли событие для брони
            //var @event = await bookingRepository.GetEventByIdAsync(booking.EventId, stoppingToken);
            //if (@event == null)
            //{
            //    booking.Reject();
            //    await bookingRepository.SaveChangesAsync(stoppingToken);
            //    _logger.LogWarning("Событие для брони '{Id}' не найдено, бронь отклоняется.", booking.Id);

            //    return;
            //}

            // 5. Подтверждаем бронь
            booking.Confirm();
            await bookingRepository.SaveChangesAsync(stoppingToken);

            // 6. Только после успешного сохранения публикуем событие
            var bookingConfirmed = new BookingConfirmed
            {
                BookingId = booking.Id,
                EventId = booking.EventId,
                UserId = booking.UserId,
                NumberOfSeats = 1,
                ConfirmedAt = booking.ProcessedAt ?? DateTime.UtcNow
            };

            await bookingEventPublisher.PublishAsync(bookingConfirmed, stoppingToken);
            _logger.LogInformation("Бронь '{Id}' подтверждена и событие опубликовано.", booking.Id);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Обработка брони '{Id}' отменена.", bookingId);
        }
        catch (Exception ex)
        {
            await RejectAndReturnSeatAsync(bookingId, stoppingToken);
            _logger.LogError(ex, "Бронирование {BookingId} отклонено из-за ошибки обработки.", bookingId);
        }
    }

    /// <summary>
    /// Отклоняет бронь и возвращает место событию.    
    /// </summary>
    private async Task RejectAndReturnSeatAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        try
        {
            // 1. Получаем репозитории в новом scope
            using var scope = _scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

            // 2. Отклоняем бронь и возвращаем место событию
            var booking = await bookingRepository.GetByIdAsync(bookingId, stoppingToken);
            if (booking != null)
            {
                booking.Reject();

                //var @event = await bookingRepository.GetEventByIdAsync(booking.EventId, stoppingToken);
                //if (@event != null)
                //    @event.ReleaseSeats();

                await bookingRepository.SaveChangesAsync(stoppingToken);
            }
        }
        catch (Exception innerEx)
        {
            _logger.LogError(innerEx, "Не удалось отклонить бронь '{Id}' и вернуть место.", bookingId);
        }
    }
}
