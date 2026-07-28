namespace Ya.Events.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
    public int EventByIdTtlMinutes { get; set; } = 5;
    public int TopEventsTtlMinutes { get; set; } = 10;
}