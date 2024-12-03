using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shortener.Shared.Services;

namespace Shortener.FrontendShared.Services;

public abstract class FrontendTelemetryBase : TelemetryBase
{
    protected FrontendTelemetryBase(IConfiguration configuration, IHostEnvironment hostEnvironment) :
        base(configuration, hostEnvironment) => JwtErrorCounter = Meter.CreateCounter<int>("jwt_errors.count");

    public Counter<int> JwtErrorCounter { get; }
}
