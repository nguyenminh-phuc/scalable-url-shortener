using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Shortener.FrontendShared.Services;

namespace Shortener.FrontendShared.Middleware;

public sealed class HttpRateLimit
{
    public string[] IgnoredPaths { get; init; } = null!;

    public RateLimitRule[] Paths { get; init; } = null!;
}

// https://redis.io/learn/develop/dotnet/aspnetcore/rate-limiting/middleware
public sealed class HttpRateLimiter
{
    private readonly Regex[] _ignoredPathRegexes;
    private readonly HttpRateLimit _limits;
    private readonly RequestDelegate _next;

    public HttpRateLimiter(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _limits = configuration.GetSection("RedisRateLimits").Get<HttpRateLimit>()!;

        _ignoredPathRegexes = new Regex[_limits.IgnoredPaths.Length];
        for (int i = 0; i < _limits.IgnoredPaths.Length; i++)
        {
            _ignoredPathRegexes[i] = new Regex(_limits.IgnoredPaths[i]);
        }
    }

    public IList<RateLimitRule> GetApplicableRules(HttpContext context)
    {
        List<RateLimitRule> applicableRules = _limits.Paths
            .Where(x => x.MatchPath(context.Request.Path))
            .OrderBy(x => x.MaxRequests)
            .GroupBy(x => new { x.PathKey, x.WindowSeconds })
            .Select(x => x.First())
            .ToList();

        return applicableRules;
    }

    public async Task InvokeAsync(HttpContext httpContext, IRateLimiterService rateLimiterService)
    {
        if (_ignoredPathRegexes.Any(ignoredPathRegex => ignoredPathRegex.IsMatch(httpContext.Request.Path)))
        {
            await _next(httpContext);
            return;
        }

        IPAddress ip = httpContext.Connection.RemoteIpAddress!;

        IList<RateLimitRule> applicableRules = GetApplicableRules(httpContext);
        if (applicableRules.Count > 0)
        {
            bool limited = await rateLimiterService.IsLimited(applicableRules, ip);
            if (limited)
            {
                httpContext.Response.StatusCode = 429;
                return;
            }
        }

        await _next(httpContext);
    }
}

public static class HttpRateLimiterExtensions
{
    public static IApplicationBuilder UseHttpRateLimiter(this IApplicationBuilder builder) =>
        builder.UseMiddleware<HttpRateLimiter>();
}
