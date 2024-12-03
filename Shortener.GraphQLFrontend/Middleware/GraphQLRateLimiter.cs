using HotChocolate.Resolvers;
using Shortener.FrontendShared.Services;

namespace Shortener.GraphQLFrontend.Middleware;

public enum RateLimitType
{
    Normal,
    Restricted
}

public sealed class GraphQLRateLimiter(FieldDelegate next, IList<RateLimitRule> rules)
{
    public async Task InvokeAsync(
        IMiddlewareContext middlewareContext,
        IHttpContextAccessor httpContext,
        IRateLimiterService rateLimiterService)
    {
        bool limited = await rateLimiterService.IsLimited(
            rules,
            httpContext.HttpContext!.Connection.RemoteIpAddress!);
        if (limited)
        {
            throw new GraphQLException("Rate limited");
        }

        await next(middlewareContext);
    }
}

public static class GraphQLRateLimiterExtensions
{
    public static IObjectFieldDescriptor UseRateLimiter(
        this IObjectFieldDescriptor descriptor,
        IConfiguration configuration,
        RateLimitType type)
    {
        if (!configuration.GetValue("RATE_LIMITER_ENABLED", false))
        {
            return descriptor;
        }

        List<RateLimitRule> rules = configuration.GetSection("RedisRateLimits").Get<RateLimitRule[]>()!.ToList();
        if (type == RateLimitType.Restricted)
        {
            rules.RemoveAll(rule => string.Equals(rule.Path, "normal", StringComparison.OrdinalIgnoreCase));
        }

        descriptor.Use((_, next) => new GraphQLRateLimiter(next, rules));

        return descriptor;
    }
}
