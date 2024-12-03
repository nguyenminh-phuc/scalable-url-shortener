using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shortener.Shared.Services;

namespace Shortener.BackendShared.Services;

public abstract class BackendTelemetryBase : TelemetryBase
{
    private readonly Counter<int> _databaseErrorCounter;
    private readonly Counter<int> _telemetryErrorCounter;

    protected BackendTelemetryBase(IConfiguration configuration, IHostEnvironment hostEnvironment) :
        base(configuration, hostEnvironment)
    {
        _databaseErrorCounter = Meter.CreateCounter<int>("database_errors.count");
        _telemetryErrorCounter = Meter.CreateCounter<int>("telemetry_errors.count");
    }

    public void AddDatabaseErrorCount()
    {
        _databaseErrorCounter.Add(1);
        ErrorCounter.Add(1);
    }

    public void AddTelemetryErrorCount()
    {
        _telemetryErrorCounter.Add(1);
        ErrorCounter.Add(1);
    }
}
