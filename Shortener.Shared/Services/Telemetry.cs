using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shortener.Shared.Utils;

namespace Shortener.Shared.Services;

public abstract class TelemetryBase : IDisposable
{
    private readonly Counter<int> _cacheErrorCounter;
    private readonly Counter<int> _grpcErrorCounter;
    private readonly Counter<int> _zookeeperErrorCounter;

    protected TelemetryBase(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        bool containerMode = configuration.GetValue("CONTAINER_MODE", false);

        ServiceName = hostEnvironment.ApplicationName;
        ServiceVersion = configuration["SERVICE_VERSION"];
        ServiceInstanceId = containerMode ? Environment.MachineName : RandomUtils.String(5);

        Meter = new Meter(ServiceName);
        ActivitySource = new ActivitySource(ServiceName);
        ErrorCounter = Meter.CreateCounter<int>("errors.count");
        _grpcErrorCounter = Meter.CreateCounter<int>("grpc_errors.count");
        _zookeeperErrorCounter = Meter.CreateCounter<int>("zookeeper_errors.count");
        _cacheErrorCounter = Meter.CreateCounter<int>("cache_errors.count");
    }

    public Counter<int> ErrorCounter { get; }

    public string ServiceName { get; }

    public string? ServiceVersion { get; }

    public string ServiceInstanceId { get; }

    public Meter Meter { get; }

    public ActivitySource ActivitySource { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void AddCacheErrorCount()
    {
        _cacheErrorCounter.Add(1);
        ErrorCounter.Add(1);
    }

    public void AddGrpcErrorCount()
    {
        _grpcErrorCounter.Add(1);
        ErrorCounter.Add(1);
    }

    public void AddZookeeperErrorCount()
    {
        _zookeeperErrorCounter.Add(1);
        ErrorCounter.Add(1);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Meter.Dispose();
        ActivitySource.Dispose();
    }
}
