using System.Security.Claims;
using System.Threading.Channels;
using FluentValidation;
using idunno.Authentication.Basic;
using MassTransit;
using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Enums;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using Shortener.Admin.Data;
using Shortener.Admin.Middleware;
using Shortener.Admin.Repositories;
using Shortener.Admin.Services;
using Shortener.Admin.Validators;
using Shortener.BackendShared.Middleware;
using Shortener.BackendShared.Services;
using Shortener.BackendShared.Utils;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;
using StackExchange.Redis;
using ConnectionStringUtils = Shortener.Admin.Utils.ConnectionStringUtils;
using SharedConnectionStringUtils = Shortener.Shared.Utils.ConnectionStringUtils;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddEnvironmentVariables()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        // https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore#experimental-support-for-grpc-requests
        ["OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_ENABLE_GRPC_INSTRUMENTATION"] = "true"
    });

CertificateUtils.Configure(builder);

builder.Services.AddControllers();

builder.Services.AddGrpc(options => { options.Interceptors.Add<ServerGrpcInterceptor>(); });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

string dbConnectionString = ConnectionStringUtils.GetPostgres(builder.Configuration);
builder.Services.AddDbContextPool<AdminDbContext>((provider, options) =>
{
    ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
    options.UseNpgsql(dbConnectionString, o => o.UseNodaTime())
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .UseLoggerFactory(loggerFactory);
});

string redisConnectionString = SharedConnectionStringUtils.GetRedis(builder.Configuration);
builder.Services.AddSingleton<IConnectionMultiplexer>(await ConnectionMultiplexer.ConnectAsync(redisConnectionString));

AddMassTransit(builder);

builder.Services.AddHealthChecks()
    .AddCheck<StartupHealthCheck>("startup_health_check", tags: ["ready"])
    .AddCheck<HealthCheck>("health_check", tags: ["ready"])
    .AddDbContextCheck<AdminDbContext>(tags: ["ready"])
    .AddRedis(redisConnectionString, tags: ["ready"]);

builder.Services.AddSingleton<IZookeeperService, ZookeeperService>();
builder.Services.AddSingleton<IShardService, ShardService>();
builder.Services.AddSingleton<IElectionService, ElectionService>();

builder.Services.AddScoped<IBannedDomainRepository, BannedDomainRepository>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IBannedDomainService, BannedDomainService>();
builder.Services.AddScoped<IUrlService, UrlService>();

builder.Services.AddSingleton<StartupHealthCheck>();
builder.Services.AddHostedService<StartupBackgroundService>();

builder.Services.AddHostedService<TelemetryService>();

builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(100, BoundedChannelFullMode.Wait));

AddBasicAuth(builder);

builder.Services.AddValidatorsFromAssemblyContaining<DomainValidator>();
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

MapHealthChecks(app);

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGrpcService<AdminService>().AllowAnonymous();

app.MapPrometheusScrapingEndpoint().AllowAnonymous();

app.Run();
return;

static void AddMassTransit(WebApplicationBuilder builder)
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();

        x.UsingRabbitMq((context, configure) =>
        {
            RabbitMqUtils.ConfigureHost(builder.Configuration, configure);

            configure.ConfigureEndpoints(context);
        });
    });
}

static void AddBasicAuth(WebApplicationBuilder builder)
{
    string basicAuthUser = builder.Configuration["ADMIN_USER"]!;
    string basicAuthPassword = builder.Configuration["ADMIN_PASSWORD"]!;
    builder.Services.AddAuthentication(BasicAuthenticationDefaults.AuthenticationScheme)
        .AddBasic(options =>
        {
            options.Realm = "Basic Authentication";
            options.Events = new BasicAuthenticationEvents
            {
                OnValidateCredentials = context =>
                {
                    if (context.Username != basicAuthUser || context.Password != basicAuthPassword)
                    {
                        return Task.CompletedTask;
                    }

                    Claim[] claims =
                    [
                        new(ClaimTypes.NameIdentifier,
                            context.Username,
                            ClaimValueTypes.String,
                            context.Options.ClaimsIssuer),
                        new(ClaimTypes.Name,
                            context.Username,
                            ClaimValueTypes.String,
                            context.Options.ClaimsIssuer)
                    ];

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                    context.Success();

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
}

static void AddSwagger(WebApplicationBuilder builder)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        // https://stackoverflow.com/a/68812931
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "BasicAuth", Version = "v1" });
        options.AddSecurityDefinition("basic",
            new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "basic",
                In = ParameterLocation.Header,
                Description = "Basic Authorization header using the Bearer scheme."
            });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "basic" }
                },
                []
            }
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
                .AddSource(DiagnosticHeaders.DefaultListenerName)
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
