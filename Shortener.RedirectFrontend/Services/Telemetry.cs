using System.Diagnostics.Metrics;
using Shortener.Shared.Services;

namespace Shortener.RedirectFrontend.Services;

public sealed class Telemetry : TelemetryBase
{
    public Telemetry(IConfiguration configuration, IHostEnvironment hostEnvironment) :
        base(configuration, hostEnvironment) =>
        RedirectCounter = Meter.CreateCounter<int>("redirects.count");

    public Counter<int> RedirectCounter { get; }
}
