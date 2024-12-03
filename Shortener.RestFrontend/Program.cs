using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Enums;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using Shortener.FrontendShared.Middleware;
using Shortener.FrontendShared.Services;
using Shortener.FrontendShared.Utils;
using Shortener.FrontendShared.Validators;
using Shortener.RestFrontend.Middleware;
using Shortener.RestFrontend.Services;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;
using StackExchange.Redis;
using ConnectionStringUtils = Shortener.Shared.Utils.ConnectionStringUtils;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();

builder.Services.AddProblemDetails();
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

builder.Services.AddValidatorsFromAssemblyContaining<ShortIdValidator>();
builder.Services.AddFluentValidationAutoValidation(config =>
{
    config.DisableBuiltInModelValidation = true;
    config.ValidationStrategy = ValidationStrategy.Annotations;
});

AddSwagger(builder);

AddTelemetry(builder);

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

if (builder.Configuration.GetValue("RATE_LIMITER_ENABLED", false))
{
    app.UseHttpRateLimiter();
}

MapHealthChecks(app);

app.UseAuthentication();
app.UseAuthorization();
app.UseJwtHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapPrometheusScrapingEndpoint().AllowAnonymous();

app.Run();
return;

static void AddSwagger(WebApplicationBuilder builder)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        // https://stackoverflow.com/a/64899768
        // Include 'SecurityScheme' to use JWT Authentication
        OpenApiSecurityScheme jwtSecurityScheme = new()
        {
            BearerFormat = "JWT",
            Name = "JWT Authentication",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            Description = "Put **_ONLY_** your JWT Bearer token on textbox below!",
            Reference = new OpenApiReference
            {
                Id = JwtBearerDefaults.AuthenticationScheme, Type = ReferenceType.SecurityScheme
            }
        };

        options.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);

        options.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtSecurityScheme, Array.Empty<string>() } });
    });
}

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
