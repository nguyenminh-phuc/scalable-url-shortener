using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shortener.Shared.Services;

namespace Shortener.FrontendShared.Services;

public sealed class StartupBackgroundService(
    ILogger<StartupBackgroundService> logger,
    IHostApplicationLifetime lifetime,
    StartupHealthCheck healthCheck,
    IShardService shardService)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await shardService.Initialize();

            healthCheck.StartupCompleted = true;
            await healthCheck.CancellationTokenSource.CancelAsync();
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Unhandled exception: {Exception}", exception);

            Environment.ExitCode = 1;
            lifetime.StopApplication();
        }
    }
}
