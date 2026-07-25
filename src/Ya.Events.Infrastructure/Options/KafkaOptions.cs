namespace Ya.Events.Infrastructure.Options;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = string.Empty;
}