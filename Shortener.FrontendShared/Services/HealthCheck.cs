using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shortener.Shared.Services;

namespace Shortener.FrontendShared.Services;

public sealed class HealthCheck(IZookeeperService zookeeperService) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(!zookeeperService.IsConnected
            ? HealthCheckResult.Unhealthy("Zookeeper connection error")
            : HealthCheckResult.Healthy());
}
