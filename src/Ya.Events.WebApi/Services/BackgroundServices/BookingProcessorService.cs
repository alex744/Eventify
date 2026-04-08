using Ya.Events.WebApi.Enums;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;

namespace Ya.Events.WebApi.Services.BackgroundServices;

public class BookingProcessorService : BackgroundService
{
    private readonly IBookingStore _bookingStorage;
    private readonly ILogger<BookingProcessorService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _processingDelay = TimeSpan.FromSeconds(3);

    public BookingProcessorService(
        IBookingStore bookingStorage,
        ILogger<BookingProcessorService> logger)
    {
        _bookingStorage = bookingStorage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingProcessorService запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pendingBookings = await _bookingStorage.GetAllPendingAsync(stoppingToken);

                foreach (var booking in pendingBookings)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    try
                    {
                        _logger.LogInformation("Обработка брони '{Id}'...", booking.Id);
                        await Task.Delay(_processingDelay, stoppingToken);  // Имитация обращения к внешней системе

                        var updatedBooking = booking with
                        {
                            Status = BookingStatus.Confirmed,
                            ProcessedAt = DateTime.UtcNow
                        };

                        await _bookingStorage.UpdateAsync(updatedBooking, stoppingToken);
                        _logger.LogInformation("Бронь '{Id}' подтверждена", booking.Id);
                    }
                    catch (BookingNotPendingException ex)
                    {
                        _logger.LogWarning(ex, "Бронь '{BookingId}' уже обработана (статус: {Status})", ex.Booking?.Id, ex.Booking?.Status);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при обработке брони '{Id}'", booking.Id);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка в основном цикле процессора.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("BookingProcessorService остановлен.");
    }
}
