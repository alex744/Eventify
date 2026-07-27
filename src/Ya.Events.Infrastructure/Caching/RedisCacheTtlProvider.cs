using Microsoft.Extensions.Options;
using Ya.Events.Application.Abstractions.Caching;
using Ya.Events.Infrastructure.Options;

namespace Ya.Events.Infrastructure.Caching;

internal sealed class RedisCacheTtlProvider : ICacheTtlProvider
{
    public TimeSpan EventByIdTtl { get; }
    public TimeSpan TopEventsTtl { get; }

    public RedisCacheTtlProvider(IOptions<RedisOptions> options)
    {
        var redisOptions = options.Value;

        if (redisOptions.EventByIdTtlMinutes <= 0)
            throw new InvalidOperationException("Redis:EventByIdTtlMinutes должно быть больше 0.");

        if (redisOptions.TopEventsTtlMinutes <= 0)
            throw new InvalidOperationException("Redis:TopEventsTtlMinutes должно быть больше 0.");

        EventByIdTtl = TimeSpan.FromMinutes(redisOptions.EventByIdTtlMinutes);
        TopEventsTtl = TimeSpan.FromMinutes(redisOptions.TopEventsTtlMinutes);
    }
}
