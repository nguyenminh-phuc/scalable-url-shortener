using Shortener.Shared.Entities;

namespace Shortener.Shared.Utils;

public static class CacheUtils
{
    public static string GetUrlMappingKey(ShortId shortId) => $"Url-Mapping:{shortId}";

    public static string GetUrlStatsKey(ShortId shortId) => $"Url-Stats:{shortId}";

    public static string GetUserHashKey(UserId userId) => $"User:{userId}";

    public static string GetPaginationEntryKey(int? first, string? after, int? last, string? before) =>
        $"{first}-{after}-{last}-{before}";

    public static string UrlCountHashKey(string domain) => $"Url-Count:{domain}";
}
