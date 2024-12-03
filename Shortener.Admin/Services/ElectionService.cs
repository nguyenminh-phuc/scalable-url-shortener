using Shortener.BackendShared.Services;
using Shortener.Shared.Services;

namespace Shortener.Admin.Services;

public sealed class ElectionService(
    IConfiguration configuration,
    TelemetryBase telemetry,
    ILoggerFactory loggerFactory,
    IZookeeperService zookeeperService)
    : ElectionServiceBase(configuration, "admin", telemetry, loggerFactory, zookeeperService);
