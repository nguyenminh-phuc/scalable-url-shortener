using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Shortener.Shared.Exceptions;
using Shortener.Shared.Services;
using Status = Google.Rpc.Status;

namespace Shortener.Shared.Middleware;

// https://github.com/grpc/grpc-dotnet/blob/master/examples/Interceptor/Client/ClientLoggerInterceptor.cs
public sealed class ClientGrpcInterceptor(TelemetryBase telemetry, ILoggerFactory loggerFactory) : Interceptor
{
    private readonly ILogger<ClientGrpcInterceptor> _logger = loggerFactory.CreateLogger<ClientGrpcInterceptor>();

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        AsyncUnaryCall<TResponse> call = continuation(request, context);
        return new AsyncUnaryCall<TResponse>(
            HandleResponse(call.ResponseAsync),
            call.ResponseHeadersAsync, call.GetStatus, call.GetTrailers, call.Dispose);
    }

    private async Task<TResponse> HandleResponse<TResponse>(Task<TResponse> t)
    {
        try
        {
            TResponse response = await t;
            return response;
        }
        catch (RpcException ex)
        {
            Status? status = ex.GetRpcStatus();
            if (status is null)
            {
                // Thrown by the client
                if (ex.StatusCode == StatusCode.Unavailable)
                {
                    telemetry.AddGrpcErrorCount();
                }
            }

            _logger.LogError(ex, "[{StatusCode}] - {Detail}: {Exception}",
                ex.StatusCode,
                ex.Status.Detail,
                ex.Status.DebugException);

            throw RpcExceptionUtils.Parse(ex);
        }
    }
}
