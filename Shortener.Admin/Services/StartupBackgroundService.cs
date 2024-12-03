using Shortener.BackendShared.Services;
using Shortener.Shared.Services;

namespace Shortener.Admin.Services;

public sealed class StartupBackgroundService(
    ILogger<StartupBackgroundService> logger,
    IHostApplicationLifetime lifetime,
    StartupHealthCheck healthCheck,
    IElectionService electionService)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await electionService.Initialize();

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
