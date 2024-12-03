namespace Shortener.Shared;

public static class Constants
{
    public static readonly uint ThresholdForNewUrls = 9_500_000;

    public static readonly uint ThresholdForNewUsers = 9_000_000;

    public static readonly char JwtSubSeparator = ':';

    public static readonly int MaxUrlsPageSize = 50;

    public static readonly string ShardPath = "/shards";

    public static readonly string ElectionPath = "/election";
}
