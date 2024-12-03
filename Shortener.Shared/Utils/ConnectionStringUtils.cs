using System.Text;
using Microsoft.Extensions.Configuration;

namespace Shortener.Shared.Utils;

public static class ConnectionStringUtils
{
    private const ushort DefaultZookeeperPort = 2181;
    private const ushort DefaultRedisPort = 6379;
    private const ushort DefaultRedisSentinelPort = 26379;
    private const bool DefaultRedisSentinelEnabled = false;
    private const string DefaultRedisServiceName = "mymaster";

    public static string GetZookeeper(IConfiguration configuration)
    {
        string? server = configuration["ZOOKEEPER_SERVER"];
        if (string.IsNullOrEmpty(server))
        {
            throw new Exception("ZOOKEEPER_SERVER is required");
        }

        ushort port = configuration.GetValue("ZOOKEEPER_PORT", DefaultZookeeperPort);

        return $"{server}:{port}";
    }

    public static string GetRedis(IConfiguration configuration)
    {
        StringBuilder sb = new();

        bool sentinelEnabled = configuration.GetValue("REDIS_SENTINEL_ENABLED", DefaultRedisSentinelEnabled);

        string? server = configuration["REDIS_SERVER"];
        if (string.IsNullOrEmpty(server))
        {
            throw new Exception("REDIS_SERVER is required");
        }

        ushort port = configuration.GetValue(
            "REDIS_PORT",
            sentinelEnabled ? DefaultRedisSentinelPort : DefaultRedisPort);
        sb.Append($"{server}:{port},");

        string? password = configuration["REDIS_PASSWORD"];
        if (!string.IsNullOrEmpty(password))
        {
            sb.Append($"password={password},");
        }

        if (sentinelEnabled)
        {
            string serviceName = configuration["REDIS_SERVICE_NAME"] ?? DefaultRedisServiceName;
            sb.Append($"serviceName={serviceName},");
        }

        sb.Append("abortConnect=false,");

        string? options = configuration["REDIS_OPTIONS"];
        if (!string.IsNullOrEmpty(options))
        {
            sb.Append(options);
        }

        return sb.ToString();
    }
}
