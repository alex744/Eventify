using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Ya.Events.Application.Abstractions.Caching;
using Ya.Events.Infrastructure.Options;
using Ya.Events.Infrastructure.Persistence;
using Ya.Shared.Contracts;
using Ya.Shared.Contracts.Events;

namespace Ya.Events.Infrastructure.Services;

internal sealed class BookingConsumerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<KafkaOptions> _options;
    private readonly ILogger<BookingConsumerWorker> _logger;

    public BookingConsumerWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<BookingConsumerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Task.Run нужен, чтобы Consume (блокирующий вызов) не блокировал хост при старте
        return Task.Run(() => ConsumeAsync(stoppingToken), stoppingToken);
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var kafkaOptions = _options.Value;

        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            GroupId = kafkaOptions.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topics.BookingConfirmed);

        _logger.LogInformation("Consumer запущен. Ожидание сообщений из топика '{Topic}'...", Topics.BookingConfirmed);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = consumer.Consume(stoppingToken);
                await ProcessBookingConfirmedAsync(consumer, consumeResult, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumer остановлен штатно.");
        }
        finally
        {
            consumer.Close();
        }
    }

    /// <summary>
    /// Обрабатывает одно сообщение о подтвержденном бронировании.
    /// </summary>    
    private async Task ProcessBookingConfirmedAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> consumeResult,
        CancellationToken stoppingToken)
    {
        try
        {
            var booking = JsonSerializer.Deserialize<BookingConfirmed>(consumeResult.Message.Value);
            if (booking is null)
            {
                _logger.LogWarning("Некорректное сообщение: {Payload}", consumeResult.Message.Value);
                consumer.StoreOffset(consumeResult);
                consumer.Commit();

                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<ICache>();

            // 1. Проверяем, что событие существует
            var existingEvent = await db.Events.FirstOrDefaultAsync(e => e.Id == booking.EventId, stoppingToken);
            if (existingEvent is null)
            {
                _logger.LogWarning("Событие с идентификатором '{EventId}' не найдено.", booking.EventId);
                consumer.StoreOffset(consumeResult);
                consumer.Commit();

                return;
            }

            // 2. Проверяем и резервируем место
            if (!existingEvent.TryReserveSeats())
            {
                _logger.LogWarning("Свободных мест на событие с идентификатором '{EventId}' нет.", booking.EventId);
                consumer.StoreOffset(consumeResult);
                consumer.Commit();

                return;
            }

            // 3. Сохраняем изменения события в базе данных
            await db.SaveChangesAsync(stoppingToken);

            // 4. Инвалидируем кэш после изменения данных
            await cache.RemoveAsync($"event:{booking.EventId}", stoppingToken);

            _logger.LogInformation("Бронирование подтверждено для события с идентификатором '{EventId}'.", booking.EventId);
            consumer.StoreOffset(consumeResult);
            consumer.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке сообщения: {Payload}", consumeResult.Message.Value);
        }
    }
}
