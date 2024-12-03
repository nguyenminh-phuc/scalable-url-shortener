using System.Net;
using NodaTime;
using NodaTime.Serialization.Protobuf;
using Shortener.FrontendShared.Services;
using Shortener.Shared.Entities;
using Shortener.Shared.Grpc;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;
using UrlServiceClient = Shortener.Shared.Grpc.UrlService.UrlServiceClient;

namespace Shortener.RedirectFrontend.Services;

public interface IRedirectService
{
    public Task<string> Redirect(
        ShortId id, IPAddress ip, string? userAgent, string? referrer,
        CancellationToken cancellationToken = default);
}

public sealed class RedirectService(
    IBackgroundTaskQueue taskQueue,
    IGrpcClientFactory grpcClientFactory,
    ICacheService cacheService) : IRedirectService
{
    public async Task<string> Redirect(
        ShortId id, IPAddress ip, string? userAgent, string? referrer,
        CancellationToken cancellationToken = default)
    {
        RedirectRequest request = new()
        {
            ShortId = id.ToString(),
            Ip = ip.ToString(),
            Timestamp = SystemClock.Instance.GetCurrentInstant().ToTimestamp()
        };

        if (userAgent is not null)
        {
            request.UserAgent = userAgent;
        }

        if (referrer is not null)
        {
            request.Referrer = referrer;
        }

        string? cachedUrl = await cacheService.GetString(CacheUtils.GetUrlMappingKey(id));
        if (cachedUrl is not null)
        {
            await NotifyRedirectInBackground(request);
            return cachedUrl;
        }

        UrlServiceClient client = grpcClientFactory.GetUrlClient(id.Range);
        RedirectReply reply = await client.RedirectAsync(request, cancellationToken: cancellationToken);

        return reply.Url.DestinationUrl;
    }

    private async Task NotifyRedirectInBackground(RedirectRequest request)
    {
        await taskQueue.QueueBackgroundWorkItem(Task);
        return;

        async ValueTask Task(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                ShortId id = ShortIdUtils.ParseId(request.ShortId);

                UrlServiceClient client = grpcClientFactory.GetUrlClient(id.Range);
                await client.NotifyRedirectAsync(request, cancellationToken: cancellationToken);
            }
        }
    }
}
