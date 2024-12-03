using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shortener.Shared.Services;

namespace Shortener.BackendShared.Services;

public sealed class HealthCheck(IElectionService electionService, IZookeeperService zookeeperService) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!electionService.IsHealthy)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Election error"));
        }

        if (!zookeeperService.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Zookeeper connection error"));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
