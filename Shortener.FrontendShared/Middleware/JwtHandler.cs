using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shortener.Shared.Entities;

namespace Shortener.FrontendShared.Middleware;

public sealed class JwtHandler(RequestDelegate next)
{
    public static readonly string UserId = "UserId";
    public static readonly string Username = "Username";

    public async Task InvokeAsync(HttpContext httpContext)
    {
        if (httpContext.User.Identity is { IsAuthenticated: true })
        {
            string? sub = httpContext.User.Claims
                .FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?
                .Value;
            if (sub is null || !Shared.Entities.UserId.TryParse(sub, out UserId? userId))
            {
                throw new Exception($"Invalid {ClaimTypes.NameIdentifier}: {sub}");
            }

            string? username = httpContext.User.Claims
                .FirstOrDefault(claim => claim.Type == ClaimTypes.Name)?
                .Value;
            if (username is null)
            {
                throw new Exception($"Invalid {ClaimTypes.Name}: {username}");
            }

            httpContext.Items[UserId] = userId;
            httpContext.Items[Username] = username;
        }

        await next(httpContext);
    }
}

public static class JwtHandlerExtensions
{
    public static IApplicationBuilder UseJwtHandler(this IApplicationBuilder builder) =>
        builder.UseMiddleware<JwtHandler>();
}
