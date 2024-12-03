using Shortener.Shared.Entities;
using Browser = Shortener.Shared.Grpc.Browser;
using Platform = Shortener.Shared.Grpc.Platform;
using UserAgent = Shortener.Shared.Grpc.UserAgent;

namespace Shortener.Shared.Extensions;

public static class UserAgentExtensions
{
    public static UserAgent Serialize(this UserAgentType type) =>
        type switch
        {
            UserAgentType.Browser => UserAgent.Browser,
            UserAgentType.Robot => UserAgent.Robot,
            _ => UserAgent.Unknown
        };

    public static Platform Serialize(this PlatformType type) =>
        type switch
        {
            PlatformType.Windows => Platform.Windows,
            PlatformType.Linux => Platform.Linux,
            PlatformType.Ios => Platform.Ios,
            PlatformType.MacOs => Platform.Macos,
            PlatformType.Android => Platform.Android,
            _ => Platform.Other
        };

    public static Browser Serialize(this BrowserType type) =>
        type switch
        {
            BrowserType.Chrome => Browser.Chrome,
            BrowserType.Edge => Browser.Edge,
            BrowserType.Firefox => Browser.Firefox,
            BrowserType.InternetExplorer => Browser.InternetExplorer,
            BrowserType.Opera => Browser.Opera,
            BrowserType.Safari => Browser.Safari,
            _ => Browser.Other
        };
}
