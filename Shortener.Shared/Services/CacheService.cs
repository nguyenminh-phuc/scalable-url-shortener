using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shortener.Shared.Utils;
using StackExchange.Redis;

namespace Shortener.Shared.Services;

public interface ICacheServiceBase
{
    Task<T?> Get<T>(string key) where T : class;

    Task<T?> GetOrNullable<T>(string key) where T : struct;

    Task<IDictionary<string, T?>?> Get<T>(IList<string> keys);

    Task<T?> GetHash<T>(string hashKey, string entryKey);

    Task<IDictionary<string, T>?> GetHashAll<T>(string hashKey);

    Task<string?> GetString(string key);

    Task Set<T>(string key, T data, TimeSpan duration);

    Task<bool> SetHash<T>(string hashKey, string entryKey, T data, TimeSpan duration);

    Task SetString(string key, string data, TimeSpan duration);

    Task<bool> Remove(string key);
}

public abstract class CacheServiceBase(
    TelemetryBase telemetry,
    ILoggerFactory loggerFactory,
    IConnectionMultiplexer muxer) : ICacheServiceBase
{
    private readonly ILogger<CacheServiceBase> _logger = loggerFactory.CreateLogger<CacheServiceBase>();
    protected readonly IConnectionMultiplexer Muxer = muxer;

    public async Task<T?> Get<T>(string key) where T : class =>
        await LogExceptions(async () =>
        {
            RedisValue serializedData = await Muxer.GetDatabase().StringGetAsync(key);
            return !serializedData.HasValue
                ? default
                : JsonSerializer.Deserialize<T>(serializedData!, JsonUtils.SerializerOptions);
        });

    public async Task<T?> GetOrNullable<T>(string key) where T : struct =>
        await LogExceptions(async () =>
        {
            RedisValue serializedData = await Muxer.GetDatabase().StringGetAsync(key);
            return !serializedData.HasValue
                ? default(T?)
                : JsonSerializer.Deserialize<T>(serializedData!, JsonUtils.SerializerOptions);
        });

    public async Task<IDictionary<string, T?>?> Get<T>(IList<string> keys) =>
        await LogExceptions(async () =>
        {
            RedisKey[] redisKeys = keys.Select(key => new RedisKey(key)).ToArray();
            RedisValue[] redisValues = await Muxer.GetDatabase().StringGetAsync(redisKeys);

            Dictionary<string, T?> cachedData = [];
            for (int i = 0; i < redisValues.Length; i++)
            {
                cachedData.Add(keys[i],
                    redisValues[i].HasValue
                        ? JsonSerializer.Deserialize<T>(redisValues[i]!, JsonUtils.SerializerOptions)
                        : default);
            }

            return cachedData.Count == 0 ? null : cachedData;
        });

    public async Task<T?> GetHash<T>(string hashKey, string entryKey) =>
        await LogExceptions(async () =>
        {
            RedisValue serializedData = await Muxer.GetDatabase().HashGetAsync(hashKey, entryKey);
            return !serializedData.HasValue
                ? default
                : JsonSerializer.Deserialize<T>(serializedData!, JsonUtils.SerializerOptions);
        });

    public async Task<IDictionary<string, T>?> GetHashAll<T>(string hashKey) =>
        await LogExceptions(async () =>
        {
            Dictionary<string, T> data = [];

            HashEntry[] entries = await Muxer.GetDatabase().HashGetAllAsync(hashKey);
            foreach (HashEntry entry in entries)
            {
                string entryKey = entry.Name!;
                T value = JsonSerializer.Deserialize<T>(entry.Value!, JsonUtils.SerializerOptions)!;
                data.Add(entryKey, value);
            }

            return data.Count != 0 ? data : null;
        });

    public async Task<string?> GetString(string key) =>
        await LogExceptions(async () =>
        {
            RedisValue cachedData = await Muxer.GetDatabase().StringGetAsync(key);
            return cachedData.HasValue ? (string)cachedData! : null;
        });

    public async Task Set<T>(string key, T data, TimeSpan duration) =>
        await LogExceptions(async () =>
        {
            string serializedData = JsonSerializer.Serialize(data, JsonUtils.SerializerOptions);
            await SetString(key, serializedData, duration);
        });

    public async Task<bool> SetHash<T>(string hashKey, string entryKey, T data, TimeSpan duration) =>
        await LogExceptions(async () =>
        {
            string serializedData = JsonSerializer.Serialize(data, JsonUtils.SerializerOptions);

            ITransaction transaction = Muxer.GetDatabase().CreateTransaction();
            _ = transaction.HashSetAsync((RedisKey)hashKey, entryKey, serializedData);
            _ = transaction.KeyExpireAsync((RedisKey)hashKey, duration);

            return await transaction.ExecuteAsync();
        });

    public async Task SetString(string key, string data, TimeSpan duration) =>
        await LogExceptions(async () => { await Muxer.GetDatabase().StringSetAsync(key, data, duration); });

    public async Task<bool> Remove(string key) =>
        await LogExceptions(async () => await Muxer.GetDatabase().KeyDeleteAsync(key));

    protected async Task<T> LogExceptions<T>(Func<Task<T>> task)
    {
        try
        {
            return await task();
        }
        catch (RedisCommandException ex)
        {
            LogError(ex);
            throw;
        }
        catch (RedisTimeoutException ex)
        {
            LogError(ex);
            throw;
        }
        catch (RedisConnectionException ex)
        {
            LogError(ex);
            throw;
        }
        catch (RedisException ex)
        {
            LogError(ex);
            throw;
        }
    }

    private async Task LogExceptions(Func<Task> task)
    {
        try
        {
            await task();
        }
        catch (RedisCommandException ex)
        {
            LogError(ex);
            throw;
        }
        catch (RedisTimeoutException ex)
        {
            LogError(ex);
            throw;
        }
        catch (RedisConnectionException ex)
        {
            LogError(ex);
            throw;
        }
        catch (RedisException ex)
        {
            LogError(ex);
            throw;
        }
    }

    private void LogError(Exception exception)
    {
        telemetry.AddCacheErrorCount();
        _logger.LogError(exception, "{Exception}", exception);
    }
}
