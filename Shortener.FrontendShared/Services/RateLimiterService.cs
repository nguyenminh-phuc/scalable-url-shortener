using System.Net;
using System.Text.RegularExpressions;
using StackExchange.Redis;

namespace Shortener.FrontendShared.Services;

// https://github.com/redis-developer/rate-limiting-middleware-aspnetcore/tree/main
public sealed partial class RateLimitRule
{
    private static readonly Regex s_timePattern = TimePatternRegex();
    private int _windowSeconds;

    internal string PathKey => !string.IsNullOrEmpty(Path) ? Path : PathRegex;
    public string Path { get; set; } = null!;
    public string PathRegex { get; set; } = null!;
    public string Window { get; set; } = null!;
    public int MaxRequests { get; set; }

    internal int WindowSeconds
    {
        get
        {
            if (_windowSeconds < 1)
            {
                _windowSeconds = ParseTime(Window);
            }

            return _windowSeconds;
        }
    }

    public bool MatchPath(string path)
    {
        if (!string.IsNullOrEmpty(Path))
        {
            return path.Equals(Path, StringComparison.InvariantCultureIgnoreCase);
        }

        if (!string.IsNullOrEmpty(PathRegex))
        {
            return Regex.IsMatch(path, PathRegex);
        }

        return false;
    }

    private static int ParseTime(string timeStr)
    {
        Match match = s_timePattern.Match(timeStr);
        if (string.IsNullOrEmpty(match.Value))
        {
            throw new ArgumentException("Rate limit window was not provided or was not " +
                                        "properly formatted, must be of the form ([0-9]+(s|m|d|h))");
        }

        TimeUnit unit = Enum.Parse<TimeUnit>(match.Value.Last().ToString());
        int num = int.Parse(match.Value[..^1]);
        return num * (int)unit;
    }

    [GeneratedRegex("([0-9]+(s|m|d|h))")]
    private static partial Regex TimePatternRegex();

    private enum TimeUnit
    {
        s = 1,
        m = 60,
        h = 3600,
        d = 86400
    }
}

public interface IRateLimiterService
{
    Task<bool> IsLimited(IList<RateLimitRule> rules, IPAddress ip);
}

public sealed class RateLimiterService(ICacheService cacheService) : IRateLimiterService
{
    public async Task<bool> IsLimited(IList<RateLimitRule> rules, IPAddress ip)
    {
        RedisKey[] keys = rules.Select(x => new RedisKey($"{x.PathKey}:{{{ip}}}:{x.WindowSeconds}")).ToArray();
        List<RedisValue> args = [rules.Count];
        foreach (RateLimitRule rule in rules)
        {
            args.Add(rule.WindowSeconds);
            args.Add(rule.MaxRequests);
        }

        return await cacheService.RateLimit(keys, args.ToArray());
    }
}
