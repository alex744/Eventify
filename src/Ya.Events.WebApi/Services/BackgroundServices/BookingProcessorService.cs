using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Services.BackgroundServices;

public class BookingProcessorService : BackgroundService
{
    private readonly IBookingStore _bookingStore;
    private readonly IEventService _eventService;
    private readonly ILogger<BookingProcessorService> _logger;
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _processingDelay = TimeSpan.FromSeconds(3);

    public BookingProcessorService(
        IBookingStore bookingStore,
        IEventService eventService,
        ILogger<BookingProcessorService> logger)
    {
        _bookingStore = bookingStore;
        _eventService = eventService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingProcessorService запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pendingBookings = await _bookingStore.GetAllPendingAsync(stoppingToken);
                var tasks = pendingBookings.Select(booking => ProcessBookingAsync(booking, stoppingToken));
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
    /// внутри семафора проверяет существование события и подтверждает или отклоняет бронь.
    /// </summary>
    private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Обработка брони '{Id}'...", booking.Id);

            // 1. Имитация внешнего запроса (выполняется параллельно для разных броней)
            await Task.Delay(_processingDelay, stoppingToken);

            // 2. Захват семафора для безопасного обновления хранилища
            await _processingSemaphore.WaitAsync(stoppingToken);
            try
            {
                // Проверяем, существует ли событие
                var evnt = await _eventService.GetByIdAsync(booking.EventId, stoppingToken);
                if (evnt is null)
                {
                    _logger.LogWarning("Событие для брони '{Id}' не найдено, бронь отклоняется.", booking.Id);
                    booking.Reject();
                    await _bookingStore.UpdateAsync(booking, stoppingToken);

                    return;
                }

                // Подтверждаем бронь
                booking.Confirm();
                await _bookingStore.UpdateAsync(booking, stoppingToken);
                _logger.LogInformation("Бронь '{Id}' подтверждена", booking.Id);
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Обработка брони '{Id}' отменена.", booking.Id);
        }
        //catch (BookingNotPendingException ex)
        //{
        //    _logger.LogWarning(ex, "Бронь '{BookingId}' уже обработана (статус: {Status})", ex.Booking?.Id, ex.Booking?.Status);
        //}
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке брони '{Id}'", booking.Id);
            await RejectAndReturnSeatAsync(booking, stoppingToken);
        }
    }

    /// <summary>
    /// Отклоняет бронь и возвращает место событию.    
    /// </summary>
    private async Task RejectAndReturnSeatAsync(Booking booking, CancellationToken ct)
    {
        try
        {
            await _processingSemaphore.WaitAsync(ct);
            try
            {
                // Отклоняем бронь
                booking.Reject();
                await _bookingStore.UpdateAsync(booking, ct);

                // Возвращаем место
                var evnt = await _eventService.GetByIdAsync(booking.EventId, ct);
                if (evnt is not null)
                {
                    evnt.ReleaseSeats();
                    await _eventService.UpdateAsync(evnt.Id, evnt, ct);
                }
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }
        catch (Exception innerEx)
        {
            _logger.LogError(innerEx, "Не удалось отклонить бронь '{Id}' и вернуть место.", booking.Id);
        }
    }
}
