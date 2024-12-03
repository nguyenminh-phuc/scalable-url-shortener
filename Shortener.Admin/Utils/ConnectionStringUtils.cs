namespace Shortener.Admin.Utils;

public static class ConnectionStringUtils
{
    private const string DefaultServer = "shortener-admin-postgresql-ha-pgpool";
    private const ushort DefaultPort = 5432;
    private const string DefaultDatabase = "postgres";
    private const string DefaultUser = "postgres";

    public static string GetPostgres(IConfiguration configuration)
    {
        string server = configuration["POSTGRESQL_SERVER"] ?? DefaultServer;
        ushort port = configuration.GetValue("POSTGRESQL_PORT", DefaultPort);
        string database = configuration["POSTGRESQL_DATABASE"] ?? DefaultDatabase;
        string user = configuration["POSTGRESQL_USER"] ?? DefaultUser;
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
}
