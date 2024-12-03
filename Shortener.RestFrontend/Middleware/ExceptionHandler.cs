using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using org.apache.zookeeper;
using Shortener.Shared.Exceptions;
using StackExchange.Redis;
using RedisConnectionException = Shortener.Shared.Exceptions.RedisConnectionException;

namespace Shortener.RestFrontend.Middleware;

public sealed class ExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails details = new()
        {
            Type = exception.GetType().Name,
            Title = "An error occurred while processing your request.",
            Status = StatusCodes.Status500InternalServerError,
            Detail = exception.Message,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
            Extensions = { { "traceId", Activity.Current?.Id ?? httpContext.TraceIdentifier } }
        };

        details.Detail = exception switch
        {
            RedisTimeoutException or StackExchange.Redis.RedisConnectionException => "Cache connection error",
            RedisCommandException or RedisException => "Cache error",
            KeeperException => "Zookeeper error",
            _ => details.Detail
        };

        details.Status = exception switch
        {
            DatabaseErrorException or RedisErrorException or ZookeeperErrorException =>
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError,
            GrpcConnectionException or RedisConnectionException =>
                httpContext.Response.StatusCode = StatusCodes.Status502BadGateway,
            NotFoundException => httpContext.Response.StatusCode = StatusCodes.Status404NotFound,
            ResourceExhaustedException => httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests,
            UnauthenticatedException => httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized,
            PermissionDeniedException => httpContext.Response.StatusCode = StatusCodes.Status403Forbidden,
            ArgumentException => httpContext.Response.StatusCode = StatusCodes.Status400BadRequest,
            _ => httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError
        };

        await httpContext.Response.WriteAsJsonAsync(details, cancellationToken);

        return true;
    }
}
