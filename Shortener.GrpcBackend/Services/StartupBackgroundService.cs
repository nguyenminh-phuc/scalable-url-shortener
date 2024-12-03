using Shortener.BackendShared.Services;
using Shortener.GrpcBackend.Repositories;
using Shortener.Shared.Services;

namespace Shortener.GrpcBackend.Services;

public sealed class StartupBackgroundService(
    ILogger<StartupBackgroundService> logger,
    IHostApplicationLifetime lifetime,
    IServiceScopeFactory serviceScopeFactory,
    StartupHealthCheck healthCheck,
    IElectionService electionService,
    IShardService shardService)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
            IRangeRepository rangeRepository = scope.ServiceProvider.GetRequiredService<IRangeRepository>();
            IUrlRepository urlRepository = scope.ServiceProvider.GetRequiredService<IUrlRepository>();

            await shardService.Initialize(rangeRepository, urlRepository);
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
