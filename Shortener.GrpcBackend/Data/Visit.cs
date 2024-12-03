using NodaTime;

namespace Shortener.GrpcBackend.Data;

public sealed class Visit
{
    public int Id { get; init; }

    public int Total { get; init; }

    public int BrowserType { get; init; }

    public int RobotType { get; init; }

    public int UnknownType { get; init; }

    public string Platforms { get; init; } = null!;

    public int Windows { get; init; }

    public int Linux { get; init; }

    public int Ios { get; init; }

    public int MacOs { get; init; }

    public int Android { get; init; }

    public int OtherPlatform { get; init; }

    public string Browsers { get; init; } = null!;

    public int Chrome { get; init; }

    public int Edge { get; init; }

    public int Firefox { get; init; }

    public int InternetExplorer { get; init; }

    public int Opera { get; init; }

    public int Safari { get; init; }

    public int OtherBrowser { get; init; }

    public string MobileDeviceTypes { get; init; } = null!;

    public string Countries { get; init; } = null!;

    public string Referrers { get; init; } = null!;

    public int UrlId { get; init; }

    public Url Url { get; init; } = null!;

    public Instant CreatedAt { get; init; }

    public Instant UpdatedAt { get; init; }
}
