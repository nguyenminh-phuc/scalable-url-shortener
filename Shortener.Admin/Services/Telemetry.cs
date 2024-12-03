using System.Diagnostics.Metrics;
using Shortener.BackendShared.Services;
using Shortener.Shared.Services;

namespace Shortener.Admin.Services;

public sealed class Telemetry(IConfiguration configuration, IHostEnvironment hostEnvironment)
    : BackendTelemetryBase(configuration, hostEnvironment);

public sealed class TelemetryService(
    ILogger<TelemetryService> logger,
    IServiceScopeFactory serviceScopeFactory,
    StartupHealthCheck healthCheck,
    Telemetry telemetry,
    IElectionService electionService)
    : BackgroundService
{
    private readonly Gauge<int> _bannedDomainCounter = telemetry.Meter.CreateGauge<int>("banned_domains.count");
    private bool _becomeSlave;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await healthCheck.WaitUntilReady(stoppingToken);

            try
            {
                if (await electionService.IsMaster())
                {
                    _becomeSlave = false;

                    await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
                    IBannedDomainService domainService =
                        scope.ServiceProvider.GetRequiredService<IBannedDomainService>();

                    _bannedDomainCounter.Record(await domainService.GetCount(stoppingToken));
                }
                else
                {
                    if (!_becomeSlave)
                    {
                        _bannedDomainCounter.Record(0);
                        _becomeSlave = true;
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                telemetry.AddTelemetryErrorCount();
                logger.LogError(ex, "{Exception}", ex);
            }
        }
    }
}
