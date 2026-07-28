using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using Ya.Events.Application.Abstractions.Caching;

namespace Ya.Events.Infrastructure.Caching;

internal sealed class RedisCache : ICache
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisCache> _logger;

    public RedisCache(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisCache> logger)
    {
        _database = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        try
        {
            RedisValue value = await _database.StringGetAsync(key);
            if (value.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при получении ключа из Redis. Ключ: {CacheKey}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);
        ct.ThrowIfCancellationRequested();

        try
        {
            string json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, ttl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при записи ключа в Redis. Ключ: {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при удалении ключа из Redis. Ключ: {CacheKey}", key);
        }
    }
}
