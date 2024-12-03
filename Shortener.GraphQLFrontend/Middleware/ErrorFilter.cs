using Shortener.Shared.Exceptions;

namespace Shortener.GraphQLFrontend.Middleware;

public sealed class ErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is null)
        {
            return error;
        }

        IError e = error.WithMessage(error.Exception.Message);

        int code = error.Exception switch
        {
            DatabaseErrorException or RedisErrorException or ZookeeperErrorException =>
                StatusCodes.Status500InternalServerError,
            GrpcConnectionException or RedisConnectionException => StatusCodes.Status502BadGateway,
            NotFoundException => StatusCodes.Status404NotFound,
            ResourceExhaustedException => StatusCodes.Status429TooManyRequests,
            UnauthenticatedException => StatusCodes.Status401Unauthorized,
            PermissionDeniedException => StatusCodes.Status403Forbidden,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        e.WithCode(code.ToString());

        return e;
    }
}
