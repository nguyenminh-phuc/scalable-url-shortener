using System.Diagnostics.Metrics;
using Shortener.BackendShared.Services;
using Shortener.GrpcBackend.Repositories;

namespace Shortener.GrpcBackend.Services;

public sealed class Telemetry(IConfiguration configuration, IHostEnvironment hostEnvironment)
    : BackendTelemetryBase(configuration, hostEnvironment);

public sealed class TelemetryService(
    ILogger<TelemetryService> logger,
    IServiceScopeFactory serviceScopeFactory,
    IElectionService electionService,
    Telemetry telemetry)
    : BackgroundService
{
    private readonly Gauge<int> _domainCounter = telemetry.Meter.CreateGauge<int>("domains.count");
    private readonly Gauge<int> _urlCounter = telemetry.Meter.CreateGauge<int>("urls.count");
    private readonly Gauge<int> _userCounter = telemetry.Meter.CreateGauge<int>("users.count");
    private bool _becomeSlave;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await electionService.IsMaster())
                {
                    _becomeSlave = false;

                    await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
                    ITelemetryRepository telemetryRepository =
                        scope.ServiceProvider.GetRequiredService<ITelemetryRepository>();

                    TelemetryCounts counts = await telemetryRepository.GetCounts(stoppingToken);
                    _domainCounter.Record(counts.DomainCount);
                    _userCounter.Record(counts.UserCount);
                    _urlCounter.Record(counts.UrlCount);
                }
                else
                {
                    if (!_becomeSlave)
                    {
                        _domainCounter.Record(0);
                        _userCounter.Record(0);
                        _urlCounter.Record(0);
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
