using Grpc.Core;

namespace Shortener.Shared.Exceptions;

public abstract class ShortenerException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class DatabaseErrorException : ShortenerException
{
    public DatabaseErrorException(string message) : base(message)
    {
    }

    public DatabaseErrorException(string message, RpcException innerException) : base(message, innerException)
    {
    }
}

public sealed class RedisErrorException(string message, RpcException innerException)
    : ShortenerException(message, innerException);

public sealed class ZookeeperErrorException(string message) : ShortenerException(message);

public sealed class GrpcConnectionException(string message, RpcException innerException) :
    ShortenerException(message, innerException);

public sealed class RedisConnectionException(string message, Exception innerException) :
    ShortenerException(message, innerException);

public sealed class NotFoundException(string resourceName, string message, RpcException innerException) :
    ShortenerException(message, innerException)
{
    public string ResourceName { get; } = resourceName;
}

public sealed class ResourceExhaustedException : ShortenerException
{
    public ResourceExhaustedException(string subject, string message) : base(message) => Subject = subject;

    public ResourceExhaustedException(string subject, string message, RpcException innerException) :
        base(message, innerException) =>
        Subject = subject;

    public string Subject { get; }
}

public sealed class UnauthenticatedException(
    Dictionary<string, string> metadata,
    string message,
    RpcException innerException)
    : ShortenerException(message, innerException)
{
    public Dictionary<string, string> Metadata { get; } = metadata;
}

public sealed class PermissionDeniedException : ShortenerException
{
    public PermissionDeniedException(Dictionary<string, string> metadata, string message) : base(message) =>
        Metadata = metadata;

    public PermissionDeniedException(Dictionary<string, string> metadata, string message, RpcException innerException)
        : base(message, innerException) =>
        Metadata = metadata;

    public Dictionary<string, string> Metadata { get; }
}

public sealed class UnknownException(string message, RpcException innerException) :
    ShortenerException(message, innerException);
