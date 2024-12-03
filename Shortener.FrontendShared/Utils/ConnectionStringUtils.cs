using Microsoft.Extensions.Configuration;

namespace Shortener.FrontendShared.Utils;

public static class ConnectionStringUtils
{
    private const string DefaultScheme = "dns";
    private const ushort DefaultPort = 443;

    public static string GetGrpc(IConfiguration configuration, long shardId)
    {
        string scheme = configuration["BACKEND_SCHEME"] ?? DefaultScheme;

        string? serverFormat = configuration["BACKEND_SERVER_FORMAT"];
        if (string.IsNullOrEmpty(serverFormat))
        {
            throw new Exception("BACKEND_SERVER_FORMAT is required");
        }

        string server = string.Format(serverFormat, shardId);
        ushort port = configuration.GetValue("BACKEND_PORT", DefaultPort);

        return string.Equals(scheme, "dns", StringComparison.OrdinalIgnoreCase)
            ? $"dns:///{server}:{port}"
            : $"{scheme}://{server}:{port}";
    }

    public static string GetScheme(IConfiguration configuration) => configuration["BACKEND_SCHEME"] ?? DefaultScheme;

    public static bool CanAcceptAnyCertificate(IConfiguration configuration) =>
        configuration.GetValue("BACKEND_ACCEPT_ANY_CERTIFICATE", false);
}
