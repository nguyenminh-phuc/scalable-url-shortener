using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shortener.Shared.Services;

public sealed class StartupHealthCheck : IHealthCheck, IDisposable
{
    private volatile bool _isReady;

    public bool StartupCompleted
    {
        get => _isReady;
        set => _isReady = value;
    }

    public CancellationTokenSource CancellationTokenSource { get; } = new();

    public void Dispose() => CancellationTokenSource.Dispose();

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(StartupCompleted
            ? HealthCheckResult.Healthy("The startup task has completed.")
            : HealthCheckResult.Unhealthy("That startup task is still running."));

    public async Task WaitUntilReady(CancellationToken stoppingToken)
    {
        while (!StartupCompleted)
        {
            try
            {
                using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken,
                    CancellationTokenSource.Token);
                await Task.Delay(TimeSpan.FromMinutes(5), linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
