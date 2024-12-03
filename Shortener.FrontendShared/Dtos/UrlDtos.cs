using Shortener.Shared.Entities;
using Shortener.Shared.Services;

namespace Shortener.FrontendShared.Dtos;

public sealed class UrlMapping
{
    public required string ShortUrl { get; init; }

    public required string DestinationUrl { get; init; }
}

public sealed class UrlStats(Shared.Entities.UrlStats stats, IShortUrlService shortUrlService)
{
    public string ShortUrl { get; } = shortUrlService.Get(stats.ShortId);

    public string DestinationUrl { get; } = stats.DestinationUrl;

    public int TotalViews { get; } = stats.TotalViews;

    public ViewStats LastDay { get; } = stats.LastDay;

    public ViewStats LastWeek { get; } = stats.LastWeek;

    public ViewStats LastMonth { get; } = stats.LastMonth;

    public ViewStats AllTime { get; } = stats.AllTime;
}

public sealed class CreateUrlInput
{
    public required string DestinationUrl { get; init; }
}

public sealed class GetUrlsInput
{
    public int? First { get; init; }

    public string? After { get; init; }

    public int? Last { get; init; }

    public string? Before { get; init; }
}

public sealed class UpdateUrlRestInput
{
    public required string DestinationUrl { get; init; }
}

public sealed class UpdateUrlInput
{
    public required string ShortUrl { get; init; }

    public required string DestinationUrl { get; init; }
}
