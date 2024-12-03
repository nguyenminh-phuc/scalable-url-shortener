using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Enum = System.Enum;
using Status = Google.Rpc.Status;

namespace Shortener.Shared.Exceptions;

public static class RpcExceptionUtils
{
    public enum ExceptionSource
    {
        Database,
        Redis,
        Zookeeper
    }

    public static RpcException NotFound(string resourceName, string? message = null)
    {
        message ??= $"{resourceName} not found";

        Status status = new()
        {
            Code = (int)Code.NotFound,
            Message = message,
            Details = { Any.Pack(new ResourceInfo { ResourceName = resourceName }) }
        };

        return status.ToRpcException();
    }

    public static RpcException ResourceExhausted(string subject, string? message = null)
    {
        message ??= $"{subject} exhausted";

        Status status = new()
        {
            Code = (int)Code.ResourceExhausted,
            Message = message,
            Details =
            {
                Any.Pack(new QuotaFailure
                {
                    Violations = { new QuotaFailure.Types.Violation { Subject = subject } }
                })
            }
        };

        return status.ToRpcException();
    }

    public static RpcException InvalidArgument(string field, string? message = null)
    {
        message ??= $"Invalid {field}";

        Status status = new()
        {
            Code = (int)Code.InvalidArgument,
            Message = message,
            Details =
            {
                Any.Pack(new BadRequest
                {
                    FieldViolations = { new BadRequest.Types.FieldViolation { Field = field } }
                })
            }
        };

        return status.ToRpcException();
    }

    public static RpcException OutOfRange(string field, string? message = null)
    {
        message ??= $"Invalid {field}";

        Status status = new()
        {
            Code = (int)Code.OutOfRange,
            Message = message,
            Details =
            {
                Any.Pack(new BadRequest
                {
                    FieldViolations = { new BadRequest.Types.FieldViolation { Field = field } }
                })
            }
        };

        return status.ToRpcException();
    }

    public static RpcException Unauthenticated(IDictionary<string, string> metadata, string? message = null)
    {
        message ??= "Unauthenticated";

        ErrorInfo errorInfo = new();
        foreach (KeyValuePair<string, string> kvp in metadata)
        {
            errorInfo.Metadata.Add(kvp.Key, kvp.Value);
        }

        Status status = new()
        {
            Code = (int)Code.Unauthenticated, Message = message, Details = { Any.Pack(errorInfo) }
        };

        return status.ToRpcException();
    }

    public static RpcException PermissionDenied(IDictionary<string, string> metadata, string? message = null)
    {
        message ??= "Permission denied";

        ErrorInfo errorInfo = new();
        foreach (KeyValuePair<string, string> kvp in metadata)
        {
            errorInfo.Metadata.Add(kvp.Key, kvp.Value);
        }

        Status status = new()
        {
            Code = (int)Code.PermissionDenied, Message = message, Details = { Any.Pack(errorInfo) }
        };

        return status.ToRpcException();
    }

    public static RpcException FailedPrecondition(ExceptionSource source, string message)
    {
        Status status = new()
        {
            Code = (int)Code.FailedPrecondition,
            Message = message,
            Details =
            {
                Any.Pack(new PreconditionFailure
                {
                    Violations = { new PreconditionFailure.Types.Violation { Type = source.ToString() } }
                })
            }
        };

        return status.ToRpcException();
    }

    public static RpcException Unavailable(ExceptionSource source, string message)
    {
        Status status = new()
        {
            Code = (int)Code.Unavailable,
            Message = message,
            Details = { Any.Pack(new DebugInfo { Detail = source.ToString() }) }
        };

        return status.ToRpcException();
    }

    public static RpcException Unknown(ExceptionSource source, string message)
    {
        Status status = new()
        {
            Code = (int)Code.Unknown,
            Message = message,
            Details = { Any.Pack(new DebugInfo { Detail = source.ToString() }) }
        };

        return status.ToRpcException();
    }

    public static Exception Parse(RpcException exception)
    {
        Status? status = exception.GetRpcStatus();
        if (status is null)
        {
            if (exception.StatusCode == StatusCode.Unavailable)
            {
                return new GrpcConnectionException("Backend connection error", exception);
            }

            return new UnknownException("Unknown error", exception);
        }

        switch (status.Code)
        {
            case (int)Code.NotFound:
            {
                ResourceInfo? details = status.GetDetail<ResourceInfo>();
                return new NotFoundException(details.ResourceName, status.Message, exception);
            }
            case (int)Code.ResourceExhausted:
            {
                QuotaFailure? detail = status.GetDetail<QuotaFailure>();
                QuotaFailure.Types.Violation? violation = detail.Violations.FirstOrDefault();
                return new ResourceExhaustedException(violation!.Subject, status.Message, exception);
            }
            case (int)Code.InvalidArgument:
            {
                BadRequest? detail = status.GetDetail<BadRequest>();
                BadRequest.Types.FieldViolation? violation = detail.FieldViolations.FirstOrDefault();
                return new ArgumentException(status.Message, violation!.Field, exception);
            }
            case (int)Code.OutOfRange:
            {
                BadRequest? detail = status.GetDetail<BadRequest>();
                BadRequest.Types.FieldViolation? violation = detail.FieldViolations.FirstOrDefault();
                string? _ = violation!.Field;
                return new ArgumentOutOfRangeException(status.Message, exception);
            }
            case (int)Code.Unauthenticated:
            {
                ErrorInfo? detail = status.GetDetail<ErrorInfo>();
                Dictionary<string, string> metadata = detail.Metadata.ToDictionary();
                return new UnauthenticatedException(metadata, status.Message, exception);
            }
            case (int)Code.PermissionDenied:
            {
                ErrorInfo? detail = status.GetDetail<ErrorInfo>();
                Dictionary<string, string> metadata = detail.Metadata.ToDictionary();
                return new PermissionDeniedException(metadata, status.Message, exception);
            }
            case (int)Code.FailedPrecondition:
            {
                PreconditionFailure? detail = status.GetDetail<PreconditionFailure>();
                PreconditionFailure.Types.Violation? violation = detail.Violations.FirstOrDefault();
                _ = Enum.Parse<ExceptionSource>(violation!.Type);
                return new DatabaseErrorException("Database logic error", exception);
            }
            case (int)Code.Unavailable:
            {
                DebugInfo? detail = status.GetDetail<DebugInfo>();
                ExceptionSource source = Enum.Parse<ExceptionSource>(detail.Detail);
                switch (source)
                {
                    case ExceptionSource.Redis:
                        return new RedisConnectionException("Cache connection error", exception);
                    case ExceptionSource.Database:
                    case ExceptionSource.Zookeeper:
                    default:
                        throw new ArgumentOutOfRangeException(nameof(source));
                }
            }
            case (int)Code.Unknown:
            {
                DebugInfo? detail = status.GetDetail<DebugInfo>();
                ExceptionSource source = Enum.Parse<ExceptionSource>(detail.Detail);
                return source switch
                {
                    ExceptionSource.Database => new DatabaseErrorException("Database error", exception),
                    ExceptionSource.Redis => new RedisErrorException("Cache error", exception),
                    ExceptionSource.Zookeeper => new RedisErrorException("ZooKeeper error", exception),
                    _ => throw new ArgumentOutOfRangeException(nameof(source))
                };
            }
        }

        return new UnknownException("Unknown error", exception);
    }
}
