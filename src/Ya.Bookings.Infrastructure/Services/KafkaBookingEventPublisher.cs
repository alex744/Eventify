using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Ya.Bookings.Application.Abstractions.Services;
using Ya.Bookings.Infrastructure.Options;
using Ya.Shared.Contracts;
using Ya.Shared.Contracts.Events;

namespace Ya.Bookings.Infrastructure.Services;

internal sealed class KafkaBookingEventPublisher : IBookingEventPublisher, IDisposable
{
    private readonly string _topic;
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaBookingEventPublisher> _logger;

    public KafkaBookingEventPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaBookingEventPublisher> logger)
    {
        var kafkaOptions = options.Value;

        _logger = logger;
        _topic = Topics.BookingConfirmed;

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            ClientId = kafkaOptions.GroupId,
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
        _logger.LogInformation("Kafka издатель инициализирован с брокером: {BootstrapServers}", kafkaOptions.BootstrapServers);
    }

    public async Task PublishAsync(BookingConfirmed message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageKey = message.EventId.ToString();
        var kafkaMessage = new Message<string, string>
        {
            Key = messageKey,
            Value = JsonSerializer.Serialize(message)
        };

        try
        {
            var result = await _producer.ProduceAsync(_topic, kafkaMessage, ct);

            if (result.Status == PersistenceStatus.Persisted)
            {
                _logger.LogInformation("Событие опубликовано в Kafka. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, Key: {Key}",
                    _topic,
                    result.Partition.Value,
                    result.Offset.Value,
                    messageKey);
            }
            else
            {
                _logger.LogWarning("Событие опубликовано не полностью. Topic: {Topic}, Status: {Status}, Key: {Key}", _topic, result.Status, messageKey);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Публикация события отменена. Topic: {Topic}, Key: {Key}", _topic, messageKey);
            throw;
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Ошибка при публикации события в Kafka. Topic: {Topic}, Key: {Key}, Reason: {Reason}", _topic, messageKey, ex.Error.Reason);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка при публикации события. Topic: {Topic}, Key: {Key}", _topic, messageKey);
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        _logger.LogInformation("Kafka издатель остановлен");
    }
}
