using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Npgsql;
using org.apache.zookeeper;
using Shortener.BackendShared.Services;
using Shortener.Shared.Exceptions;
using StackExchange.Redis;
using RedisConnectionException = StackExchange.Redis.RedisConnectionException;
using Status = Google.Rpc.Status;

namespace Shortener.BackendShared.Middleware;

// https://github.com/grpc/grpc-dotnet/blob/master/examples/Interceptor/Server/ServerLoggerInterceptor.cs
public sealed class ServerGrpcInterceptor(BackendTelemetryBase telemetry, ILogger<ServerGrpcInterceptor> logger)
    : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (PostgresException ex)
        {
            telemetry.AddDatabaseErrorCount();
            throw RpcExceptionUtils.FailedPrecondition(RpcExceptionUtils.ExceptionSource.Database, ex.ToString());
        }
        catch (NpgsqlException ex)
        {
            telemetry.AddDatabaseErrorCount();
            throw RpcExceptionUtils.Unknown(RpcExceptionUtils.ExceptionSource.Database, ex.ToString());
        }
        catch (RedisCommandException ex)
        {
            throw RpcExceptionUtils.Unknown(RpcExceptionUtils.ExceptionSource.Redis, ex.ToString());
        }
        catch (RedisTimeoutException ex)
        {
            throw RpcExceptionUtils.Unavailable(RpcExceptionUtils.ExceptionSource.Redis, ex.ToString());
        }
        catch (RedisConnectionException ex)
        {
            throw RpcExceptionUtils.Unavailable(RpcExceptionUtils.ExceptionSource.Redis, ex.ToString());
        }
        catch (RedisException ex)
        {
            throw RpcExceptionUtils.Unknown(RpcExceptionUtils.ExceptionSource.Redis, ex.ToString());
        }
        catch (KeeperException ex)
        {
            throw RpcExceptionUtils.Unknown(RpcExceptionUtils.ExceptionSource.Zookeeper, ex.ToString());
        }
        catch (ArgumentNullException ex)
        {
            telemetry.ErrorCounter.Add(1);
            throw RpcExceptionUtils.InvalidArgument(ex.ParamName ?? "unknown", ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            telemetry.ErrorCounter.Add(1);
            throw RpcExceptionUtils.OutOfRange(ex.ParamName ?? "unknown", ex.Message);
        }
        catch (ArgumentException ex)
        {
            telemetry.ErrorCounter.Add(1);
            throw RpcExceptionUtils.InvalidArgument(ex.ParamName ?? "unknown", ex.Message);
        }
        catch (RpcException ex)
        {
            Status? status = ex.GetRpcStatus();
            if (status is null) // not thrown by app
            {
                telemetry.AddGrpcErrorCount();
            }

            throw;
        }
        catch (Exception ex)
        {
            telemetry.ErrorCounter.Add(1);

            // Note: The gRPC framework also logs exceptions thrown by handlers to .NET Core logging.
            logger.LogError(ex, "Unhandled exception: {Exception}", ex);

            throw;
        }
    }
}
