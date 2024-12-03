using Microsoft.Extensions.Configuration;

namespace Shortener.Shared.Utils;

public static class OtlpUtils
{
    public static string? GetEndpoint(IConfiguration config)
    {
        bool enabled = config.GetValue("OTLP_ENABLED", false);
        if (!enabled)
        {
            return null;
        }

        string? endpoint = config["OTLP_ENDPOINT"];
        if (string.IsNullOrEmpty(endpoint))
        {
            throw new Exception("OTLP_ENDPOINT is required");
        }

        return endpoint;
    }
}
