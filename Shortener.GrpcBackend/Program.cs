using System.Threading.Channels;
using Grpc.Core;
using Grpc.Net.Client.Balancer;
using Grpc.Net.Client.Configuration;
using MassTransit;
using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyCSharp.HttpUserAgentParser.MemoryCache.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shortener.BackendShared.Middleware;
using Shortener.BackendShared.Services;
using Shortener.BackendShared.Utils;
using Shortener.GrpcBackend.Consumers;
using Shortener.GrpcBackend.Data;
using Shortener.GrpcBackend.Repositories;
using Shortener.GrpcBackend.Services;
using Shortener.Shared.Middleware;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;
using StackExchange.Redis;
using AdminService = Shortener.Shared.Grpc.AdminService;
using CacheService = Shortener.BackendShared.Services.CacheService;
using DomainService = Shortener.GrpcBackend.Services.DomainService;
using ICacheService = Shortener.BackendShared.Services.ICacheService;
using IDomainService = Shortener.GrpcBackend.Services.IDomainService;
using TelemetryBase = Shortener.Shared.Services.TelemetryBase;
using SharedConnectionStringUtils = Shortener.Shared.Utils.ConnectionStringUtils;
using StatusCode = Grpc.Core.StatusCode;
using Telemetry = Shortener.GrpcBackend.Services.Telemetry;
using TelemetryService = Shortener.GrpcBackend.Services.TelemetryService;
using UrlService = Shortener.GrpcBackend.Services.UrlService;
using User = Shortener.GrpcBackend.Data.User;
using UserService = Shortener.GrpcBackend.Services.UserService;
using IBackendShardService = Shortener.GrpcBackend.Services.IShardService;
using BackendShardService = Shortener.GrpcBackend.Services.ShardService;
using ConnectionStringUtils = Shortener.GrpcBackend.Utils.ConnectionStringUtils;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddEnvironmentVariables()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        // https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore#experimental-support-for-grpc-requests
        ["OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_GRPC_INSTRUMENTATION"] = "true"
    });

CertificateUtils.Configure(builder);

builder.Services.AddGrpc(options => { options.Interceptors.Add<ServerGrpcInterceptor>(); });

string dbConnectionString = ConnectionStringUtils.GetBackendPostgres(builder.Configuration);
builder.Services.AddDbContextPool<BackendDbContext>((provider, options) =>
{
    ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
    options.UseNpgsql(dbConnectionString, o => o.UseNodaTime())
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .UseLoggerFactory(loggerFactory);
});

builder.Services.AddHttpUserAgentMemoryCachedParser(options =>
{
    options.CacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(60);
    options.CacheOptions.SizeLimit = 1024;
});

string redisConnectionString = SharedConnectionStringUtils.GetRedis(builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(await ConnectionMultiplexer.ConnectAsync(redisConnectionString));

AddGrpcClient(builder);

AddMassTransit(builder);

builder.Services.AddHealthChecks()
    .AddCheck<StartupHealthCheck>("startup_health_check", tags: ["ready"])
    .AddCheck<HealthCheck>("health_check", tags: ["ready"])
    .AddDbContextCheck<BackendDbContext>(tags: ["ready"])
    .AddRedis(redisConnectionString, tags: ["ready"]);

builder.Services.AddSingleton<IGeoIpService, GeoIpService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher>();
builder.Services.AddSingleton<IZookeeperService, ZookeeperService>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IElectionService, ElectionService>();
builder.Services.AddSingleton<IBackendShardService, BackendShardService>();

builder.Services.AddScoped<IDomainRepository, DomainRepository>();
builder.Services.AddScoped<IRangeRepository, RangeRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IVisitRepository, VisitRepository>();
builder.Services.AddScoped<ITelemetryRepository, TelemetryRepository>();
builder.Services.AddScoped<IUrlRepository, UrlRepository>();
builder.Services.AddScoped<IDomainService, DomainService>();
builder.Services.AddScoped<ICacheService, CacheService>();

builder.Services.AddSingleton<StartupHealthCheck>();
builder.Services.AddHostedService<StartupBackgroundService>();

builder.Services.AddHostedService<TelemetryService>();

builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(100, BoundedChannelFullMode.Wait));

AddTelemetry(builder);

WebApplication app = builder.Build();

MapHealthChecks(app);

app.MapGrpcService<UrlService>();
app.MapGrpcService<UserService>();

app.MapPrometheusScrapingEndpoint().AllowAnonymous();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
return;

static void AddGrpcClient(WebApplicationBuilder builder)
{
    string scheme = ConnectionStringUtils.GetGrpcScheme(builder.Configuration);
    if (string.Equals(scheme, "dns", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddSingleton<ResolverFactory>(_ => new DnsResolverFactory(TimeSpan.FromMinutes(5)));
    }

    builder.Services.AddSingleton<ClientGrpcInterceptor>();

    builder.Services.AddGrpcClient<AdminService.AdminServiceClient>(o =>
        {
            string connectionString = ConnectionStringUtils.GetGrpc(builder.Configuration);
            o.Address = new Uri(connectionString);
        })
        .ConfigureChannel(options =>
        {
            MethodConfig defaultMethodConfig = new()
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = 5,
                    InitialBackoff = TimeSpan.FromSeconds(1),
                    MaxBackoff = TimeSpan.FromSeconds(5),
                    BackoffMultiplier = 1.5,
                    RetryableStatusCodes = { StatusCode.Unavailable }
                }
            };

            if (string.Equals(scheme, "dns", StringComparison.OrdinalIgnoreCase))
            {
                options.Credentials = ChannelCredentials.SecureSsl;
                options.ServiceConfig = new ServiceConfig
                {
                    MethodConfigs = { defaultMethodConfig }, LoadBalancingConfigs = { new RoundRobinConfig() }
                };
            }
            else
            {
                options.ServiceConfig = new ServiceConfig { MethodConfigs = { defaultMethodConfig } };
            }

            if (ConnectionStringUtils.CanAcceptAnyCertificate(builder.Configuration))
            {
                HttpClientHandler handler = new();
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                options.HttpHandler = handler;
            }
        })
        .AddInterceptor<ClientGrpcInterceptor>()
        .EnableCallContextPropagation();
}

static void AddMassTransit(WebApplicationBuilder builder)
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();

        x.AddConsumer<GetUrlCountConsumer>();

        x.UsingRabbitMq((context, configure) =>
        {
            RabbitMqUtils.ConfigureHost(builder.Configuration, configure);

            configure.ConfigureEndpoints(context);
        });
    });
}

static void AddTelemetry(WebApplicationBuilder builder)
{
    Telemetry telemetry = new(builder.Configuration, builder.Environment);
    builder.Services.AddSingleton(telemetry);
    builder.Services.AddSingleton<BackendTelemetryBase>(telemetry);
    builder.Services.AddSingleton<TelemetryBase>(provider => provider.GetRequiredService<Telemetry>());

    string? otlpEndpoint = OtlpUtils.GetEndpoint(builder.Configuration);
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            telemetry.ServiceName,
            serviceVersion: telemetry.ServiceVersion,
            serviceInstanceId: telemetry.ServiceInstanceId))
        .WithMetrics(metrics => metrics
            .AddMeter(telemetry.Meter.Name)
            .AddMeter(InstrumentationOptions.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddPrometheusExporter())
        .WithTracing(tracing =>
        {
            tracing.AddAspNetCoreInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddRedisInstrumentation()
                .AddSource(telemetry.ActivitySource.Name)
                .AddSource(DiagnosticHeaders.DefaultListenerName);

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
