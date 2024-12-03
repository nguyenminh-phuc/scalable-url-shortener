using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shortener.Shared.Services;

// https://learn.microsoft.com/en-us/dotnet/core/extensions/queue-service
public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItem(Func<IServiceScopeFactory, CancellationToken, ValueTask> workItem);

    ValueTask<Func<IServiceScopeFactory, CancellationToken, ValueTask>> Dequeue(CancellationToken cancellationToken);
}

public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceScopeFactory, CancellationToken, ValueTask>> _queue;

    public BackgroundTaskQueue(int capacity, BoundedChannelFullMode mode)
    {
        BoundedChannelOptions options = new(capacity) { FullMode = mode };

        _queue = Channel.CreateBounded<Func<IServiceScopeFactory, CancellationToken, ValueTask>>(options);
    }

    public async ValueTask QueueBackgroundWorkItem(Func<IServiceScopeFactory, CancellationToken, ValueTask> workItem) =>
        await _queue.Writer.WriteAsync(workItem);

    public async ValueTask<Func<IServiceScopeFactory, CancellationToken, ValueTask>>
        Dequeue(CancellationToken cancellationToken)
    {
        Func<IServiceScopeFactory, CancellationToken, ValueTask> workItem =
            await _queue.Reader.ReadAsync(cancellationToken);

        return workItem;
    }
}

public sealed class QueuedHostedService(
    ILogger<QueuedHostedService> logger,
    IServiceScopeFactory serviceScopeFactory,
    IBackgroundTaskQueue taskQueue,
    TelemetryBase telemetry,
    StartupHealthCheck healthCheck)
    : BackgroundService
{
    private readonly Counter<int> _queueErrorCounter = telemetry.Meter.CreateCounter<int>("queue_errors.count");

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => ProcessTaskQueue(stoppingToken);

    private async Task ProcessTaskQueue(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await healthCheck.WaitUntilReady(stoppingToken);

            try
            {
                Func<IServiceScopeFactory, CancellationToken, ValueTask> workItem =
                    await taskQueue.Dequeue(stoppingToken);

                await workItem(serviceScopeFactory, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if stoppingToken was signaled
            }
            catch (Exception ex)
            {
                _queueErrorCounter.Add(1);
                telemetry.ErrorCounter.Add(1);

                logger.LogError(ex, "{Exception}", ex);
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken) => await base.StopAsync(stoppingToken);
}
