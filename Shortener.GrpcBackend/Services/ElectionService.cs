using Shortener.BackendShared.Services;
using Shortener.Shared.Services;

namespace Shortener.GrpcBackend.Services;

public sealed class ElectionService(
    IConfiguration configuration,
    TelemetryBase telemetry,
    ILoggerFactory loggerFactory,
    IZookeeperService zookeeperService)
    : ElectionServiceBase(
        configuration,
        GetElectionChildPath(configuration),
        telemetry,
        loggerFactory,
        zookeeperService)
{
    private static string GetElectionChildPath(IConfiguration configuration)
    {
        if (!long.TryParse(configuration["SHARD_ID"], out long id))
        {
            throw new Exception("SHARD_ID is required");
        }

        return $"backend{id}";
    }
}
