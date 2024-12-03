using Shortener.Shared.Grpc;
using BrowserType = Shortener.Shared.Entities.BrowserType;
using PlatformType = Shortener.Shared.Entities.PlatformType;
using UserAgentType = Shortener.Shared.Entities.UserAgentType;

namespace Shortener.Shared.Utils;

public static class GrpcUtils
{
    public static UserAgentType Deserialize(UserAgent type) =>
        type switch
        {
            UserAgent.Browser => UserAgentType.Browser,
            UserAgent.Robot => UserAgentType.Robot,
            _ => UserAgentType.Unknown
        };

    public static PlatformType Deserialize(Platform type) =>
        type switch
        {
            Platform.Windows => PlatformType.Windows,
            Platform.Linux => PlatformType.Linux,
            Platform.Ios => PlatformType.Ios,
            Platform.Macos => PlatformType.MacOs,
            Platform.Android => PlatformType.Android,
            _ => PlatformType.Other
        };

    public static BrowserType Deserialize(Browser type) =>
        type switch
        {
            Browser.Chrome => BrowserType.Chrome,
            Browser.Edge => BrowserType.Edge,
            Browser.Firefox => BrowserType.Firefox,
            Browser.InternetExplorer => BrowserType.InternetExplorer,
            Browser.Opera => BrowserType.Opera,
            Browser.Safari => BrowserType.Safari,
            _ => BrowserType.Other
        };
}
