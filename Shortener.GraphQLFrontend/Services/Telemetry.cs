using Shortener.FrontendShared.Services;

namespace Shortener.GraphQLFrontend.Services;

public sealed class Telemetry(IConfiguration configuration, IHostEnvironment hostEnvironment) :
    FrontendTelemetryBase(configuration, hostEnvironment);
