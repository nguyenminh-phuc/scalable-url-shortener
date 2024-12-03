using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Shortener.BackendShared.Utils;

public static class CertificateUtils
{
    public static void Configure(WebApplicationBuilder builder)
    {
        bool enabled = builder.Configuration.GetValue("CERTIFICATE_ENABLED", false);
        string? certPath = builder.Configuration["CERTIFICATE_CERT_PATH"];
        string? keyPath = builder.Configuration["CERTIFICATE_KEY_PATH"];

        if (!enabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(certPath))
        {
            throw new Exception("CERTIFICATE_CERT_PATH is required");
        }

        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException("CERTIFICATE_CERT_PATH not found", certPath);
        }

        if (string.IsNullOrEmpty(keyPath))
        {
            throw new Exception("CERTIFICATE_KEY_PATH is required");
        }

        if (!File.Exists(keyPath))
        {
            throw new FileNotFoundException("CERTIFICATE_KEY_PATH not found", keyPath);
        }

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureHttpsDefaults(listenOptions =>
            {
                listenOptions.ServerCertificate = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            });
        });
    }
}
