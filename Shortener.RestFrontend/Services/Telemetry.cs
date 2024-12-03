using Shortener.FrontendShared.Services;

namespace Shortener.RestFrontend.Services;

public sealed class Telemetry(IConfiguration configuration, IHostEnvironment hostEnvironment) :
    FrontendTelemetryBase(configuration, hostEnvironment);
