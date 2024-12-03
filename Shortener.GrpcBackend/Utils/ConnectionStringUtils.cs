namespace Shortener.GrpcBackend.Utils;

public static class ConnectionStringUtils
{
    private const ushort DefaultPostgresPort = 5432;
    private const string DefaultPostgresDatabase = "postgres";
    private const string DefaultPostgresUser = "postgres";
    private const string DefaultAdminServer = "shortener-admin";
    private const string DefaultAdminScheme = "dns";
    private const ushort DefaultAdminPort = 443;

    public static string GetBackendPostgres(IConfiguration configuration)
    {
        string? server = configuration["POSTGRESQL_SERVER"];
        if (string.IsNullOrEmpty(server))
        {
            throw new Exception("POSTGRESQL_SERVER is required");
        }

        ushort port = configuration.GetValue("POSTGRESQL_PORT", DefaultPostgresPort);
        string database = configuration["POSTGRESQL_DATABASE"] ?? DefaultPostgresDatabase;
        string user = configuration["POSTGRESQL_USER"] ?? DefaultPostgresUser;

        string? password = configuration["POSTGRESQL_PASSWORD"];
        if (string.IsNullOrEmpty(password))
        {
            throw new Exception("POSTGRESQL_PASSWORD is required");
        }

        string? options = configuration["POSTGRESQL_OPTIONS"];
        if (string.IsNullOrEmpty(options))
        {
            options = "";
        }

        return $"Server={server};Port={port};Database={database};User ID={user};Password={password};{options}";
    }

    public static string GetGrpc(IConfiguration configuration)
    {
        string scheme = configuration["ADMIN_SCHEME"] ?? DefaultAdminScheme;
        string server = configuration["ADMIN_SERVER"] ?? DefaultAdminServer;
        ushort port = configuration.GetValue("ADMIN_PORT", DefaultAdminPort);

        return string.Equals(scheme, "dns", StringComparison.OrdinalIgnoreCase)
            ? $"dns:///{server}:{port}"
            : $"{scheme}://{server}:{port}";
    }

    public static string GetGrpcScheme(IConfiguration configuration) =>
        configuration["ADMIN_SCHEME"] ?? DefaultAdminScheme;

    public static bool CanAcceptAnyCertificate(IConfiguration configuration) =>
        configuration.GetValue("ADMIN_ACCEPT_ANY_CERTIFICATE", false);
}
