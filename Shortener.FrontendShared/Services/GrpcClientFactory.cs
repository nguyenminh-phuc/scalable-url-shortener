using Grpc.Core;
using Grpc.Net.Client.Balancer;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Shortener.FrontendShared.Utils;
using Shortener.Shared.Middleware;
using ClientFactory = Grpc.Net.ClientFactory.GrpcClientFactory;
using UrlServiceClient = Shortener.Shared.Grpc.UrlService.UrlServiceClient;
using UserServiceClient = Shortener.Shared.Grpc.UserService.UserServiceClient;

namespace Shortener.FrontendShared.Services;

public interface IGrpcClientFactory
{
    UserServiceClient GetUserClient(long shardId);

    UrlServiceClient GetUrlClient(long shardId);
}

public sealed class GrpcClientFactory(ClientFactory clientFactory) : IGrpcClientFactory
{
    public UserServiceClient GetUserClient(long shardId) =>
        clientFactory.CreateClient<UserServiceClient>(GetUserClientName(shardId));

    public UrlServiceClient GetUrlClient(long shardId) =>
        clientFactory.CreateClient<UrlServiceClient>(GetUrlClientName(shardId));

    public static void AddClients(WebApplicationBuilder builder)
    {
        if (!long.TryParse(builder.Configuration["TOTAL_SHARDS"], out long shards))
        {
            throw new Exception("TOTAL_SHARDS is required");
        }

        string scheme = ConnectionStringUtils.GetScheme(builder.Configuration);
        if (string.Equals(scheme, "dns", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<ResolverFactory>(_ => new DnsResolverFactory(TimeSpan.FromMinutes(5)));
        }

        for (long i = 0; i < shards; i++)
        {
            string connectionString = ConnectionStringUtils.GetGrpc(builder.Configuration, i);

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

            HttpClientHandler handler = new();
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            builder.Services
                .AddGrpcClient<UserServiceClient>(GetUserClientName(i), o => { o.Address = new Uri(connectionString); })
                .ConfigureChannel(options =>
                {
                    if (string.Equals(scheme, "dns", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Credentials = ChannelCredentials.SecureSsl;
                        options.ServiceConfig = new ServiceConfig
                        {
                            MethodConfigs = { defaultMethodConfig },
                            LoadBalancingConfigs = { new RoundRobinConfig() }
                        };
                    }
                    else
                    {
                        options.ServiceConfig = new ServiceConfig { MethodConfigs = { defaultMethodConfig } };
                    }

                    if (ConnectionStringUtils.CanAcceptAnyCertificate(builder.Configuration))
                    {
                        options.HttpHandler = handler;
                    }
                })
                .AddInterceptor<ClientGrpcInterceptor>();

            builder.Services
                .AddGrpcClient<UrlServiceClient>(GetUrlClientName(i), o => { o.Address = new Uri(connectionString); })
                .ConfigureChannel(options =>
                {
                    if (string.Equals(scheme, "dns", StringComparison.OrdinalIgnoreCase))
                    {
                        options.Credentials = ChannelCredentials.SecureSsl;
                        options.ServiceConfig = new ServiceConfig
                        {
                            MethodConfigs = { defaultMethodConfig },
                            LoadBalancingConfigs = { new RoundRobinConfig() }
                        };
                    }
                    else
                    {
                        options.ServiceConfig = new ServiceConfig { MethodConfigs = { defaultMethodConfig } };
                    }

                    if (ConnectionStringUtils.CanAcceptAnyCertificate(builder.Configuration))
                    {
                        options.HttpHandler = handler;
                    }
                })
                .AddInterceptor<ClientGrpcInterceptor>();
        }

        builder.Services.AddSingleton<ClientGrpcInterceptor>();
    }

    private static string GetUserClientName(long shardId) => $"Client{shardId}";

    private static string GetUrlClientName(long shardId) => $"Url{shardId}";
}
