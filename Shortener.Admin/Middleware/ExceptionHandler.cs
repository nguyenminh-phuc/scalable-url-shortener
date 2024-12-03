using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Shortener.Admin.Services;
using Shortener.Shared.Exceptions;
using StackExchange.Redis;
using RedisConnectionException = StackExchange.Redis.RedisConnectionException;

namespace Shortener.Admin.Middleware;

public sealed class ExceptionHandler(Telemetry telemetry) : IExceptionHandler
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

        switch (exception)
        {
            case NpgsqlException:
                telemetry.AddDatabaseErrorCount();
                break;
            case RedisCommandException:
            case RedisTimeoutException:
            case RedisException:
                telemetry.AddCacheErrorCount();
                break;
            default:
                telemetry.ErrorCounter.Add(1);
                break;
        }

        details.Detail = exception switch
        {
            PostgresException => "Database error",
            NpgsqlException => "Database connection error",
            RedisTimeoutException or RedisConnectionException => "Cache connection error",
            RedisCommandException or RedisException => "Cache error",
            _ => details.Detail
        };

        details.Status = exception switch
        {
            NpgsqlException or RedisTimeoutException or RedisConnectionException =>
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
