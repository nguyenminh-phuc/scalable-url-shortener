using Shortener.Shared.Extensions;
using Shortener.Shared.Grpc;
using Shortener.Shared.Utils;

namespace Shortener.Shared.Entities;

public sealed class UrlStats
{
    public UrlStats()
    {
    }

    public UrlStats(GrpcUrlStats url)
    {
        ShortId = url.ShortId;
        DestinationUrl = url.DestinationUrl;
        TotalViews = url.TotalViews;
        LastDay = new ViewStats(url.LastDay);
        LastWeek = new ViewStats(url.LastWeek);
        LastMonth = new ViewStats(url.LastMonth);
        AllTime = new ViewStats(url.AllTime);
    }

    public string ShortId { get; init; } = null!;

    public string DestinationUrl { get; init; } = null!;

    public int TotalViews { get; init; }

    public ViewStats LastDay { get; init; } = null!;

    public ViewStats LastWeek { get; init; } = null!;

    public ViewStats LastMonth { get; init; } = null!;

    public ViewStats AllTime { get; init; } = null!;

    public GrpcUrlStats Serialize() =>
        new()
        {
            ShortId = ShortId,
            DestinationUrl = DestinationUrl,
            TotalViews = TotalViews,
            LastDay = LastDay.Serialize(),
            LastWeek = LastWeek.Serialize(),
            LastMonth = LastMonth.Serialize(),
            AllTime = AllTime.Serialize()
        };
}

public sealed class PlatformStats
{
    public required PlatformType Type { get; init; }

    public required int Views { get; set; }

    public required Dictionary<string, int> VariantViews { get; init; }
}

public sealed class BrowserStats
{
    public required BrowserType Type { get; init; }

    public required int Views { get; set; }

    public required Dictionary<string, int> VersionViews { get; init; }
}

public sealed class ViewStats
{
    public ViewStats()
    {
    }

    public ViewStats(GrpcViewStats stats)
    {
        Dictionary<UserAgentType, int> types = [];
        foreach (UserAgentEntry entry in stats.Types_)
        {
            types.Add(GrpcUtils.Deserialize(entry.Type), entry.Views);
        }

        Dictionary<PlatformType, PlatformStats> platforms = [];
        foreach (PlatformEntry entry in stats.Platforms)
        {
            PlatformStats platformStats = new()
            {
                Type = GrpcUtils.Deserialize(entry.Type),
                Views = entry.Views,
                VariantViews = entry.VariantViews.ToDictionary(v => v.Key, v => v.Value)
            };
            platforms.Add(platformStats.Type, platformStats);
        }

        Dictionary<BrowserType, BrowserStats> browsers = [];
        foreach (BrowserEntry entry in stats.Browsers)
        {
            BrowserStats platformStats = new()
            {
                Type = GrpcUtils.Deserialize(entry.Type),
                Views = entry.Views,
                VersionViews = entry.VersionViews.ToDictionary(v => v.Key, v => v.Value)
            };
            browsers.Add(platformStats.Type, platformStats);
        }

        Types = types;
        Platforms = platforms;
        Browsers = browsers;
        MobileDeviceTypes = stats.MobileDeviceTypes.ToDictionary(t => t.Name, t => t.Views);
        Countries = stats.Countries.ToDictionary(c => c.Name, c => c.Views);
        Referrers = stats.Referrers.ToDictionary(r => r.Name, r => r.Views);
        Views = stats.Views;
    }

    public Dictionary<UserAgentType, int> Types { get; } = [];

    public Dictionary<PlatformType, PlatformStats> Platforms { get; } = [];

    public Dictionary<BrowserType, BrowserStats> Browsers { get; } = [];

    public Dictionary<string, int> MobileDeviceTypes { get; } = [];

    public Dictionary<string, int> Countries { get; } = [];

    public Dictionary<string, int> Referrers { get; } = [];

    public int Views { get; set; }

    public GrpcViewStats Serialize()
    {
        GrpcViewStats grpcStats = new();

        foreach ((UserAgentType type, int views) in Types)
        {
            UserAgentEntry entry = new() { Type = type.Serialize(), Views = views };
            grpcStats.Types_.Add(entry);
        }

        foreach ((PlatformType type, PlatformStats platformStats) in Platforms)
        {
            PlatformEntry entry = new()
            {
                Type = type.Serialize(), Views = platformStats.Views, VariantViews = { platformStats.VariantViews }
            };
            grpcStats.Platforms.Add(entry);
        }

        foreach ((BrowserType type, BrowserStats browserStats) in Browsers)
        {
            BrowserEntry entry = new()
            {
                Type = type.Serialize(), Views = browserStats.Views, VersionViews = { browserStats.VersionViews }
            };
            grpcStats.Browsers.Add(entry);
        }

        foreach ((string name, int views) in MobileDeviceTypes)
        {
            grpcStats.MobileDeviceTypes.Add(new MobileDeviceTypeEntry { Name = name, Views = views });
        }

        foreach ((string name, int views) in Countries)
        {
            grpcStats.Countries.Add(new CountryEntry { Name = name, Views = views });
        }

        foreach ((string name, int views) in Referrers)
        {
            grpcStats.Referrers.Add(new ReferrerEntry { Name = name, Views = views });
        }

        grpcStats.Views = Views;

        return grpcStats;
    }

    public void AddType(UserAgentType type, int views)
    {
        if (!Types.TryAdd(type, views))
        {
            Types[type] += views;
        }
    }

    public void AddPlatform(PlatformType type, int views, Dictionary<string, int> variantViews)
    {
        if (Platforms.TryGetValue(type, out PlatformStats? platformStats))
        {
            platformStats.Views += views;
            foreach ((string variant, int viewsInVariant) in variantViews)
            {
                if (!platformStats.VariantViews.TryAdd(variant, viewsInVariant))
                {
                    platformStats.VariantViews[variant] += viewsInVariant;
                }
            }
        }
        else
        {
            Platforms.Add(type, new PlatformStats { Type = type, Views = views, VariantViews = variantViews });
        }
    }

    public void AddBrowser(BrowserType type, int views, Dictionary<string, int> versionViews)
    {
        if (Browsers.TryGetValue(type, out BrowserStats? browserStats))
        {
            browserStats.Views += views;
            foreach ((string version, int viewsInVersion) in versionViews)
            {
                if (!browserStats.VersionViews.TryAdd(version, viewsInVersion))
                {
                    browserStats.VersionViews[version] += viewsInVersion;
                }
            }
        }
        else
        {
            Browsers.Add(type, new BrowserStats { Type = type, Views = views, VersionViews = versionViews });
        }
    }

    public void AddMobileDeviceType(string type, int views)
    {
        if (!MobileDeviceTypes.TryAdd(type, views))
        {
            MobileDeviceTypes[type] += views;
        }
    }

    public void AddCountry(string country, int views)
    {
        if (!Countries.TryAdd(country, views))
        {
            Countries[country] += views;
        }
    }

    public void AddReferrer(string referrer, int views)
    {
        if (!Referrers.TryAdd(referrer, views))
        {
            Referrers[referrer] += views;
        }
    }
}
