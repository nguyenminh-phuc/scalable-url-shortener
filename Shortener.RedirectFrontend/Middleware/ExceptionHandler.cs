using Microsoft.AspNetCore.Diagnostics;

namespace Shortener.RedirectFrontend.Middleware;

public sealed class ExceptionHandler(ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError("{Exception}", exception);

        httpContext.Response.Redirect("/Home/Index");
        return ValueTask.FromResult(true);
    }
}
