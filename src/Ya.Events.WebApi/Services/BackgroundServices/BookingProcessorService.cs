using Microsoft.EntityFrameworkCore;
using Ya.Events.WebApi.DataAccess;
using Ya.Events.WebApi.Enums;

namespace Ya.Events.WebApi.Services.BackgroundServices;

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
                List<Guid> pendingBookingIds;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    pendingBookingIds = await context.Bookings
                        .Where(b => b.Status == BookingStatus.Pending)
                        .Select(b => b.Id)
                        .ToListAsync(stoppingToken);
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

            // 2. Получаем контекст для доступа к данным
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 3. Получаем бронь и проверяем её статус
            var booking = await context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, stoppingToken);
            if (booking == null || booking.Status != BookingStatus.Pending)
                return;

            // 4. Проверяем, существует ли событие для брони
            var @event = await context.Events.FirstOrDefaultAsync(e => e.Id == booking.EventId, stoppingToken);
            if (@event == null)
            {
                booking.Reject();
                await context.SaveChangesAsync(stoppingToken);
                _logger.LogWarning("Событие для брони '{Id}' не найдено, бронь отклоняется.", booking.Id);

                return;
            }

            // 5. Подтверждаем бронь
            booking.Confirm();
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Бронь '{Id}' подтверждена", booking.Id);
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
            // 1. Получаем контекст для доступа к данным
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 2. Отклоняем бронь и возвращаем место событию
            var booking = await context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, stoppingToken);
            if (booking != null)
            {
                booking.Reject();

                var @event = await context.Events.FirstOrDefaultAsync(e => e.Id == booking.EventId, stoppingToken);
                if (@event != null)
                    @event.ReleaseSeats();

                await context.SaveChangesAsync(stoppingToken);
            }
        }
        catch (Exception innerEx)
        {
            _logger.LogError(innerEx, "Не удалось отклонить бронь '{Id}' и вернуть место.", bookingId);
        }
    }
}
