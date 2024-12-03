using Microsoft.Extensions.Logging;
using Shortener.Shared.Services;
using StackExchange.Redis;

namespace Shortener.BackendShared.Services;

public interface ICacheService : ICacheServiceBase;

public sealed class CacheService(
    TelemetryBase telemetry,
    ILoggerFactory loggerFactory,
    IConnectionMultiplexer muxer) :
    CacheServiceBase(telemetry, loggerFactory, muxer), ICacheService;
