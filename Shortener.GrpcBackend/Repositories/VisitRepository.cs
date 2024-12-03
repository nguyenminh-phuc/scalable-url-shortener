using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shortener.GrpcBackend.Data;
using Shortener.Shared.Entities;

namespace Shortener.GrpcBackend.Repositories;

public interface IVisitRepository
{
    Task<bool> Add(
        int urlId, string? country, UserAgent? userAgent, string? referrerHost,
        CancellationToken cancellationToken);

    Task<IList<Visit>?> GetByUrlId(int urlId, CancellationToken cancellationToken);
}

public sealed class VisitRepository(BackendDbContext context) : IVisitRepository
{
    public async Task<bool> Add(
        int urlId, string? country, UserAgent? userAgent, string? referrerHost,
        CancellationToken cancellationToken)
    {
        country = string.IsNullOrEmpty(country) ? "unknown" : country.ToLowerInvariant();

        if (!string.IsNullOrEmpty(referrerHost))
        {
            referrerHost = referrerHost.ToLowerInvariant();
        }

        Visit? existingVisit = await GetLastHour(urlId, cancellationToken);
        if (existingVisit is null)
        {
            return await Insert(urlId, country, userAgent, referrerHost, cancellationToken);
        }

        return await Update(existingVisit, country, userAgent, referrerHost, cancellationToken);
    }

    public async Task<IList<Visit>?> GetByUrlId(int urlId, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             SELECT * FROM "Visit" WHERE "UrlId" = {urlId}
             """;

        List<Visit> visits = await context.Visits.FromSql(query).ToListAsync(cancellationToken);
        return visits.Count == 0 ? null : visits;
    }

    private async Task<Visit?> GetLastHour(int urlId, CancellationToken cancellationToken)
    {
        FormattableString query =
            $"""
             SELECT * FROM "Visit"
             WHERE "UrlId" = {urlId} AND "CreatedAt" >= (NOW() - INTERVAL '1 hour')
             LIMIT 1
             """;

        return await context.Visits.FromSql(query).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> Insert(
        int urlId, string country, UserAgent? userAgent, string? referrerHost,
        CancellationToken cancellationToken)
    {
        int browserType = 0;
        int robotType = 0;
        int unknownType = 0;
        int windows = 0;
        int linux = 0;
        int ios = 0;
        int macOs = 0;
        int android = 0;
        int otherPlatform = 0;
        int chrome = 0;
        int edge = 0;
        int firefox = 0;
        int internetExplorer = 0;
        int opera = 0;
        int safari = 0;
        int otherBrowser = 0;
        Dictionary<string, int> platforms = [];
        Dictionary<string, int> browsers = [];
        Dictionary<string, int> mobileDeviceTypes = [];

        if (userAgent is not null)
        {
            switch (userAgent.Type)
            {
                case UserAgentType.Browser:
                    browserType = 1;
                    break;
                case UserAgentType.Robot:
                    robotType = 1;
                    break;
                case UserAgentType.Unknown:
                default:
                    unknownType = 1;
                    break;
            }

            if (userAgent.Platform is not null)
            {
                switch (userAgent.Platform.Type)
                {
                    case PlatformType.Windows:
                        windows = 1;
                        break;
                    case PlatformType.Linux:
                        linux = 1;
                        break;
                    case PlatformType.Ios:
                        ios = 1;
                        break;
                    case PlatformType.MacOs:
                        macOs = 1;
                        break;
                    case PlatformType.Android:
                        android = 1;
                        break;
                    case PlatformType.Other:
                    default:
                        otherPlatform = 1;
                        break;
                }

                if (userAgent.Platform.Type != PlatformType.Android && userAgent.Platform.Type != PlatformType.Ios)
                {
                    platforms.Add(userAgent.Platform.Name, 1);
                }
            }

            if (userAgent.Browser is not null)
            {
                switch (userAgent.Browser.Type)
                {
                    case BrowserType.Chrome:
                        chrome = 1;
                        break;
                    case BrowserType.Edge:
                        edge = 1;
                        break;
                    case BrowserType.Firefox:
                        firefox = 1;
                        break;
                    case BrowserType.InternetExplorer:
                        internetExplorer = 1;
                        break;
                    case BrowserType.Opera:
                        opera = 1;
                        break;
                    case BrowserType.Safari:
                        safari = 1;
                        break;
                    case BrowserType.Other:
                    default:
                        otherBrowser = 1;
                        break;
                }

                string browserVersion = $"{userAgent.Browser.Name}-{userAgent.Browser.Version ?? "unknown"}";
                browsers.Add(browserVersion, 1);
            }

            if (userAgent.MobileDeviceType is not null)
            {
                mobileDeviceTypes.Add(userAgent.MobileDeviceType, 1);
            }
        }
        else
        {
            unknownType = 1;
        }

        Dictionary<string, int> countries = new() { { country, 1 } };

        Dictionary<string, int> referrers = [];
        if (referrerHost is not null)
        {
            referrers.Add(referrerHost, 1);
        }

        FormattableString query =
            $"""
             INSERT INTO "Visit" ("Total",
                                  "BrowserType", "RobotType", "UnknownType",
                                  "Platforms", "Windows", "Linux", "Ios", "MacOs", "Android", "OtherPlatform",
                                  "Browsers", "Chrome", "Edge", "Firefox", "InternetExplorer", "Opera", "Safari", "OtherBrowser",
                                  "MobileDeviceTypes",
                                  "Countries",
                                  "Referrers",
                                  "UrlId",
                                  "CreatedAt", "UpdatedAt")
             VALUES (1,
                    {browserType}, {robotType}, {unknownType},
                    CAST({JsonSerializer.Serialize(platforms)} as jsonb), {windows}, {linux}, {ios}, {macOs}, {android}, {otherPlatform},
                    CAST({JsonSerializer.Serialize(browsers)} as jsonb), {chrome}, {edge}, {firefox}, {internetExplorer}, {opera}, {safari}, {otherBrowser},
                    CAST({JsonSerializer.Serialize(mobileDeviceTypes)} as jsonb),
                    CAST({JsonSerializer.Serialize(countries)} as jsonb),
                    CAST({JsonSerializer.Serialize(referrers)} as jsonb),
                    {urlId},
                    NOW(), NOW())
             """;

        int rowsAffected = await context.Database.ExecuteSqlAsync(query, cancellationToken);

        return rowsAffected > 0;
    }

    private async Task<bool> Update(
        Visit visit, string country, UserAgent? userAgent, string? referrerHost,
        CancellationToken cancellationToken)
    {
        bool browserType = false;
        bool robotType = false;
        bool unknownType = false;
        bool windows = false;
        bool linux = false;
        bool ios = false;
        bool macOs = false;
        bool android = false;
        bool otherPlatform = false;
        bool chrome = false;
        bool edge = false;
        bool firefox = false;
        bool internetExplorer = false;
        bool opera = false;
        bool safari = false;
        bool otherBrowser = false;
        string? platformQuery = null;
        string? browserQuery = null;
        string? mobileDeviceTypeQuery = null;
        List<NpgsqlParameter> parameters = [];

        if (userAgent is not null)
        {
            switch (userAgent.Type)
            {
                case UserAgentType.Browser:
                    browserType = true;
                    break;
                case UserAgentType.Robot:
                    robotType = true;
                    break;
                case UserAgentType.Unknown:
                default:
                    unknownType = true;
                    break;
            }

            if (userAgent.Platform is not null)
            {
                switch (userAgent.Platform.Type)
                {
                    case PlatformType.Windows:
                        windows = true;
                        break;
                    case PlatformType.Linux:
                        linux = true;
                        break;
                    case PlatformType.Ios:
                        ios = true;
                        break;
                    case PlatformType.MacOs:
                        macOs = true;
                        break;
                    case PlatformType.Android:
                        android = true;
                        break;
                    case PlatformType.Other:
                    default:
                        otherPlatform = true;
                        break;
                }

                platformQuery =
                    """
                    "Platforms" = "Platforms" || jsonb_build_object(@platform, COALESCE(("Platforms"->>@platform)::int, 0) + 1),
                    """;
                parameters.Add(new NpgsqlParameter<string>("@platform", userAgent.Platform.Name));
            }

            if (userAgent.Browser is not null)
            {
                switch (userAgent.Browser.Type)
                {
                    case BrowserType.Chrome:
                        chrome = true;
                        break;
                    case BrowserType.Edge:
                        edge = true;
                        break;
                    case BrowserType.Firefox:
                        firefox = true;
                        break;
                    case BrowserType.InternetExplorer:
                        internetExplorer = true;
                        break;
                    case BrowserType.Opera:
                        opera = true;
                        break;
                    case BrowserType.Safari:
                        safari = true;
                        break;
                    case BrowserType.Other:
                    default:
                        otherBrowser = true;
                        break;
                }

                browserQuery =
                    """
                    "Browsers" = "Browsers" || jsonb_build_object(@browserVersion, COALESCE(("Browsers"->>@browserVersion)::int, 0) + 1),
                    """;
                string browserVersion = $"{userAgent.Browser.Name}-{userAgent.Browser.Version ?? "unknown"}";
                parameters.Add(new NpgsqlParameter<string>("@browserVersion", browserVersion));
            }

            if (userAgent.MobileDeviceType is not null)
            {
                mobileDeviceTypeQuery =
                    """
                    "MobileDeviceTypes" = "MobileDeviceTypes" || jsonb_build_object(@mobileDeviceType, COALESCE(("MobileDeviceTypes"->>@mobileDeviceType)::int, 0) + 1),
                    """;
                parameters.Add(new NpgsqlParameter<string>("@mobileDeviceType", userAgent.MobileDeviceType));
            }
        }
        else
        {
            unknownType = true;
        }

        string query =
            $"""
             UPDATE "Visit"
             SET "Total" = "Total" + 1,
                {(browserType ? "\"BrowserType\" = \"BrowserType\" + 1," : "")}
                {(robotType ? "\"RobotType\" = \"RobotType\" + 1," : "")}
                {(unknownType ? "\"UnknownType\" = \"UnknownType\" + 1," : "")}
                {platformQuery ?? ""}
                {(windows ? "\"Windows\" = \"Windows\" + 1," : "")}
                {(linux ? "\"Linux\" = \"Linux\" + 1," : "")}
                {(ios ? "\"Ios\" = \"Ios\" + 1," : "")}
                {(macOs ? "\"MacOs\" = \"MacOs\" + 1," : "")}
                {(android ? "\"Android\" = \"Android\" + 1," : "")}
                {(otherPlatform ? "\"OtherPlatform\" = \"OtherPlatform\" + 1," : "")}
                {browserQuery ?? ""}
                {(chrome ? "\"Chrome\" = \"Chrome\" + 1," : "")}
                {(edge ? "\"Edge\" = \"Edge\" + 1," : "")}
                {(firefox ? "\"Firefox\" = \"Firefox\" + 1," : "")}
                {(internetExplorer ? "\"InternetExplorer\" = \"InternetExplorer\" + 1," : "")}
                {(opera ? "\"Opera\" = \"Opera\" + 1," : "")}
                {(safari ? "\"Safari\" = \"Safari\" + 1," : "")}
                {(otherBrowser ? "\"OtherBrowser\" = \"OtherBrowser\" + 1," : "")}
                {mobileDeviceTypeQuery ?? ""}
                "Countries" = "Countries" || jsonb_build_object(@country, COALESCE(("Countries"->>@country)::int, 0) + 1),
                {(referrerHost is not null ? "\"Referrers\" = \"Referrers\" || jsonb_build_object(@referrer, COALESCE((\"Referrers\"->>@referrer)::int, 0) + 1)," : "")}
                "UpdatedAt" = NOW()
             WHERE "Id" = @id
             """;

        parameters.Add(new NpgsqlParameter<string>("@country", country));
        parameters.Add(new NpgsqlParameter<int>("@id", visit.Id));
        if (referrerHost is not null)
        {
            parameters.Add(new NpgsqlParameter<string>("@referrer", referrerHost));
        }

        int rowsAffected = await context.Database.ExecuteSqlRawAsync(query, parameters, cancellationToken);

        return rowsAffected > 0;
    }
}
