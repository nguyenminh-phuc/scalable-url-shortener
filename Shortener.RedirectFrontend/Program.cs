using System.Threading.Channels;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Enums;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using Shortener.FrontendShared.Middleware;
using Shortener.FrontendShared.Services;
using Shortener.FrontendShared.Validators;
using Shortener.RedirectFrontend.Middleware;
using Shortener.RedirectFrontend.Services;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;
using StackExchange.Redis;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllersWithViews();
builder.Services.AddExceptionHandler<ExceptionHandler>();

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
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IRedirectService, RedirectService>();

if (builder.Configuration.GetValue("RATE_LIMITER_ENABLED", false))
{
    builder.Services.AddScoped<IRateLimiterService, RateLimiterService>();
}

builder.Services.AddSingleton<StartupHealthCheck>();
builder.Services.AddHostedService<StartupBackgroundService>();

builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(100, BoundedChannelFullMode.Wait));

builder.Services.AddValidatorsFromAssemblyContaining<ShortIdValidator>();
builder.Services.AddFluentValidationAutoValidation(config =>
{
    config.DisableBuiltInModelValidation = true;
    config.ValidationStrategy = ValidationStrategy.Annotations;
});

AddTelemetry(builder);

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStatusCodePages();

if (builder.Configuration.GetValue("RATE_LIMITER_ENABLED", false))
{
    app.UseHttpRateLimiter();
}

MapHealthChecks(app);

app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    "default",
    "{controller=Home}/{action=Index}/{id?}");

app.MapPrometheusScrapingEndpoint().AllowAnonymous();

app.Run();
return;

static void AddTelemetry(WebApplicationBuilder builder)
{
    Telemetry telemetry = new(builder.Configuration, builder.Environment);
    builder.Services.AddSingleton(telemetry);
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
