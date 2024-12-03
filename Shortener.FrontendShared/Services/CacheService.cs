using Microsoft.Extensions.Logging;
using Shortener.Shared.Services;
using StackExchange.Redis;

namespace Shortener.FrontendShared.Services;

public interface ICacheService : ICacheServiceBase
{
    Task<bool> RateLimit(RedisKey[] keys, RedisValue[] args);
}

public sealed class CacheService(
    TelemetryBase telemetry,
    ILoggerFactory loggerFactory,
    IConnectionMultiplexer muxer) :
    CacheServiceBase(telemetry, loggerFactory, muxer), ICacheService
{
    private const string RateLimiterScript =
        """
        local current_time = redis.call('TIME')
        local num_windows = ARGV[1]
        for i=2, num_windows*2, 2 do
            local window = ARGV[i]
            local max_requests = ARGV[i+1]
            local curr_key = KEYS[i/2]
            local trim_time = tonumber(current_time[1]) - window
            redis.call('ZREMRANGEBYSCORE', curr_key, 0, trim_time)
            local request_count = redis.call('ZCARD',curr_key)
            if request_count >= tonumber(max_requests) then
                return 1
            end
        end
        for i=2, num_windows*2, 2 do
            local curr_key = KEYS[i/2]
            local window = ARGV[i]
            redis.call('ZADD', curr_key, current_time[1], current_time[1] .. current_time[2])
            redis.call('EXPIRE', curr_key, window)
        end
        return 0
        """;

    public async Task<bool> RateLimit(RedisKey[] keys, RedisValue[] args) =>
        await LogExceptions(async () =>
            (int)await Muxer.GetDatabase().ScriptEvaluateAsync(RateLimiterScript, keys, args) == 1);
}
