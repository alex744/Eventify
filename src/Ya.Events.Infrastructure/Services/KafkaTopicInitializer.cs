using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ya.Events.Infrastructure.Options;
using Ya.Shared.Contracts;

namespace Ya.Events.Infrastructure.Services;

internal sealed class KafkaTopicInitializer : IHostedService
{
    private readonly IOptions<KafkaOptions> _options;
    private readonly ILogger<KafkaTopicInitializer> _logger;

    public KafkaTopicInitializer(
        IOptions<KafkaOptions> options,
        ILogger<KafkaTopicInitializer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var kafka = _options.Value;

        if (string.IsNullOrWhiteSpace(kafka.BootstrapServers))
        {
            _logger.LogWarning("Kafka BootstrapServers не задан. Создание топика пропущено.");
            return;
        }

        try
        {
            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = kafka.BootstrapServers
            };

            using var adminClient = new AdminClientBuilder(adminConfig).Build();

            await adminClient.CreateTopicsAsync(
                new[]
                {
                    new TopicSpecification
                    {
                        Name = Topics.BookingConfirmed,
                        NumPartitions = 1,
                        ReplicationFactor = 1
                    }
                });

            _logger.LogInformation("Топик '{Topic}' создан.", Topics.BookingConfirmed);
        }
        catch (CreateTopicsException ex) when (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            _logger.LogInformation("Топик '{Topic}' уже существует. Пропускаем создание.", Topics.BookingConfirmed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось создать топик '{Topic}'. Запуск сервиса продолжится.", Topics.BookingConfirmed);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}