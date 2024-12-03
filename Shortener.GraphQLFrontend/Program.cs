using FluentValidation;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shortener.FrontendShared.Middleware;
using Shortener.FrontendShared.Services;
using Shortener.FrontendShared.Utils;
using Shortener.FrontendShared.Validators;
using Shortener.GraphQLFrontend.GraphQL;
using Shortener.GraphQLFrontend.Middleware;
using Shortener.GraphQLFrontend.Services;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;
using StackExchange.Redis;
using ConnectionStringUtils = Shortener.Shared.Utils.ConnectionStringUtils;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddGraphQLServer()
    .AddAuthorization()
    .AddErrorFilter<ErrorFilter>()
    .AddQueryType<QueryType>()
    .AddMutationType<MutationType>()
    .AddMutationConventions()
    .AddFairyBread()
    .AddInstrumentation();

builder.Services.AddHttpContextAccessor();

string redisConnectionString = ConnectionStringUtils.GetRedis(builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(await ConnectionMultiplexer.ConnectAsync(redisConnectionString));

GrpcClientFactory.AddClients(builder);

builder.Services.AddHealthChecks()
    .AddCheck<StartupHealthCheck>("startup_health_check", tags: ["ready"])
    .AddCheck<HealthCheck>("health_check", tags: ["ready"])
    .AddRedis(redisConnectionString, tags: ["ready"]);

builder.Services.AddSingleton<IGrpcClientFactory, GrpcClientFactory>();
builder.Services.AddSingleton<IZookeeperService, ZookeeperService>();
builder.Services.AddSingleton<IShardService, ShardService>();
builder.Services.AddSingleton<IShortUrlService, ShortUrlService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUrlService, UrlService>();

if (builder.Configuration.GetValue("RATE_LIMITER_ENABLED", false))
{
    builder.Services.AddScoped<IRateLimiterService, RateLimiterService>();
}

builder.Services.AddSingleton<StartupHealthCheck>();
builder.Services.AddHostedService<StartupBackgroundService>();

JwtUtils.Configure(builder);

builder.Services.AddValidatorsFromAssemblyContaining<ShortUrlValidator>();

AddTelemetry(builder);

WebApplication app = builder.Build();

MapHealthChecks(app);

app.UseAuthentication();
app.UseAuthorization();
app.UseJwtHandler();

app.MapGraphQL();

app.MapPrometheusScrapingEndpoint().AllowAnonymous();

app.Run();
return;

static void AddTelemetry(WebApplicationBuilder builder)
{
    Telemetry telemetry = new(builder.Configuration, builder.Environment);
    builder.Services.AddSingleton(telemetry);
    builder.Services.AddSingleton<FrontendTelemetryBase>(provider => provider.GetRequiredService<Telemetry>());
    builder.Services.AddSingleton<TelemetryBase>(provider => provider.GetRequiredService<Telemetry>());

    string? otlpEndpoint = OtlpUtils.GetEndpoint(builder.Configuration);
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            telemetry.ServiceName,
            serviceVersion: telemetry.ServiceVersion,
            serviceInstanceId: telemetry.ServiceInstanceId))
        .WithMetrics(metrics => metrics
            .AddMeter(telemetry.Meter.Name)
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddPrometheusExporter())
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRedisInstrumentation()
                .AddHotChocolateInstrumentation()
                .AddSource(telemetry.ActivitySource.Name);

            if (otlpEndpoint is not null)
            {
                tracing.AddOtlpExporter(otlpOptions => { otlpOptions.Endpoint = new Uri(otlpEndpoint); });
            }
            else
            {
                tracing.AddConsoleExporter();
            }
        })
        .WithLogging(logging => logging.AddConsoleExporter());
}

static void MapHealthChecks(WebApplication app)
{
    app.MapHealthChecks(
            "/healthz/ready",
            new HealthCheckOptions { Predicate = healthCheck => healthCheck.Tags.Contains("ready") })
        .WithMetadata(new AllowAnonymousAttribute());
    app.MapHealthChecks(
            "/healthz/live",
            new HealthCheckOptions { Predicate = _ => false })
        .WithMetadata(new AllowAnonymousAttribute());
}
