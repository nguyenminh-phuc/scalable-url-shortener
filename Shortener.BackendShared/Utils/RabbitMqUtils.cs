using MassTransit;
using Microsoft.Extensions.Configuration;

namespace Shortener.BackendShared.Utils;

public static class RabbitMqUtils
{
    private const ushort DefaultPort = 5672;
    private const string DefaultVirtualHost = "/";
    private const string DefaultUser = "user";

    public static void ConfigureHost(IConfiguration configuration, IRabbitMqBusFactoryConfigurator configure)
    {
        string? server = configuration["RABBITMQ_SERVER"];
        if (string.IsNullOrEmpty(server))
        {
            throw new Exception("RABBITMQ_SERVER is required");
        }

        ushort port = configuration.GetValue("RABBITMQ_PORT", DefaultPort);
        string virtualHost = configuration["RABBITMQ_VIRTUAL_HOST"] ?? DefaultVirtualHost;
        string username = configuration["RABBITMQ_USER"] ?? DefaultUser;

        string? password = configuration["RABBITMQ_PASSWORD"];
        if (string.IsNullOrEmpty(password))
        {
            throw new Exception("RABBITMQ_PASSWORD is required");
        }

        configure.Host(server, port, virtualHost, host =>
        {
            host.Username(username);
            host.Password(password);
        });
    }
}
