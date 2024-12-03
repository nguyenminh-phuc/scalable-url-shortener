using MyCSharp.HttpUserAgentParser;

namespace Shortener.Shared.Entities;

public enum UserAgentType
{
    Unknown,
    Browser,
    Robot
}

public enum PlatformType
{
    Windows,
    Linux,
    Ios,
    MacOs,
    Android,
    Other
}

public enum BrowserType
{
    Chrome,
    Edge,
    Firefox,
    InternetExplorer,
    Opera,
    Safari,
    Other
}

public sealed record Platform(PlatformType Type, string Name);

public sealed record Browser(BrowserType Type, string Name, string? Version);

public sealed record UserAgent(UserAgentType Type, Platform? Platform, Browser? Browser, string? MobileDeviceType)
{
    public static UserAgent Parse(HttpUserAgentInformation info)
    {
        UserAgentType type = info.Type switch
        {
            HttpUserAgentType.Browser => UserAgentType.Browser,
            HttpUserAgentType.Robot => UserAgentType.Robot,
            _ => UserAgentType.Unknown
        };

        Platform? platform = null;
        if (info.Platform is not null)
        {
            PlatformType platformType = info.Platform.Value.PlatformType switch
            {
                HttpUserAgentPlatformType.Windows => PlatformType.Windows,
                HttpUserAgentPlatformType.Linux => PlatformType.Linux,
                HttpUserAgentPlatformType.IOS => PlatformType.Ios,
                HttpUserAgentPlatformType.MacOS => PlatformType.MacOs,
                HttpUserAgentPlatformType.Android => PlatformType.Android,
                _ => PlatformType.Other
            };

            platform = new Platform(platformType, info.Platform.Value.Name);
        }

        Browser? browser = null;
        if (info.Name is not null)
        {
            BrowserType browserType = info.Name switch
            {
                "Chrome" => BrowserType.Chrome,
                "Edge" => BrowserType.Edge,
                "Firefox" => BrowserType.Firefox,
                "Internet Explorer" => BrowserType.InternetExplorer,
                "Opera" => BrowserType.Opera,
                "Safari" => BrowserType.Safari,
                _ => BrowserType.Other
            };
            browser = new Browser(browserType, info.Name, info.Version);
        }

        return new UserAgent(type, platform, browser, info.MobileDeviceType);
    }
}
