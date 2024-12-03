using Shortener.Shared.Entities;
using Shortener.Shared.Exceptions;
using Shortener.Shared.Grpc;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;
using DbUrlStats = Shortener.Shared.Entities.UrlStats;
using UrlMapping = Shortener.FrontendShared.Dtos.UrlMapping;
using UrlServiceClient = Shortener.Shared.Grpc.UrlService.UrlServiceClient;
using UrlStats = Shortener.FrontendShared.Dtos.UrlStats;
using UrlUtils = Shortener.FrontendShared.Utils.UrlUtils;

namespace Shortener.FrontendShared.Services;

public interface IUrlService
{
    public Task<UrlMapping> Create(UserId userId, string destinationUrl, CancellationToken cancellationToken = default);

    public Task<UrlStats> GetById(ShortId id, CancellationToken cancellationToken = default);

    public Task<IDictionary<string, UrlStats>> GetByIds(
        IList<ShortId> ids,
        CancellationToken cancellationToken = default);

    public Task<Connection<UrlStats>> GetByUserId(
        UserId userId, int? first, string? after, int? last, string? before,
        CancellationToken cancellationToken = default);

    public Task<bool> Update(
        ShortId id, UserId userId, string destinationUrl,
        CancellationToken cancellationToken = default);

    public Task<bool> Delete(ShortId id, UserId userId, CancellationToken cancellationToken = default);
}

public sealed class UrlService(
    IGrpcClientFactory grpcClientFactory,
    ICacheService cacheService,
    IShardService shardService,
    IShortUrlService shortUrlService) :
    IUrlService
{
    public async Task<UrlMapping> Create(
        UserId userId, string destinationUrl,
        CancellationToken cancellationToken = default)
    {
        if (!shardService.CanShardCreateNewUrl(userId.ShardId))
        {
            throw new ResourceExhaustedException(nameof(ShortId), "Service unavailable");
        }

        if (!destinationUrl.StartsWith("http://") && !destinationUrl.StartsWith("https://"))
        {
            destinationUrl = $"https://{destinationUrl}";
        }

        UrlServiceClient client = grpcClientFactory.GetUrlClient(userId.ShardId);

        CreateUrlRequest rpcRequest = new() { UserId = userId.Id, DestinationUrl = destinationUrl };
        CreateUrlReply reply =
            await client.CreateAsync(rpcRequest, cancellationToken: cancellationToken);

        return new UrlMapping { ShortUrl = shortUrlService.Get(reply.Url.ShortId), DestinationUrl = destinationUrl };
    }

    public async Task<UrlStats> GetById(ShortId id, CancellationToken cancellationToken = default)
    {
        DbUrlStats? cachedUrl = await cacheService.Get<DbUrlStats>(CacheUtils.GetUrlStatsKey(id));
        if (cachedUrl is not null)
        {
            return new UrlStats(cachedUrl, shortUrlService);
        }

        UrlServiceClient client = grpcClientFactory.GetUrlClient(id.Range);

        GetUrlByIdRequest request = new() { ShortId = id.ToString() };
        GetUrlByIdReply reply =
            await client.GetByIdAsync(request, cancellationToken: cancellationToken);

        DbUrlStats url = new(reply.Url);

        return new UrlStats(url, shortUrlService);
    }

    public async Task<IDictionary<string, UrlStats>> GetByIds(
        IList<ShortId> ids,
        CancellationToken cancellationToken = default)
    {
        if (!ids.Any())
        {
            throw new ArgumentException("Invalid ids", nameof(ids));
        }

        long shardId = ids.First().Range;
        if (ids.Any(id => id.Range != shardId))
        {
            throw new ArgumentException("Invalid ids", nameof(ids));
        }

        List<string> cacheKeys = ids.Select(CacheUtils.GetUrlStatsKey).ToList();
        List<string> idsToQuery = ids.Select(id => id.ToString()).ToList();
        Dictionary<string, UrlStats> urls = [];

        IDictionary<string, DbUrlStats?>? cachedUrls = await cacheService.Get<DbUrlStats>(cacheKeys);
        if (cachedUrls is not null)
        {
            foreach ((string id, DbUrlStats? url) in cachedUrls)
            {
                if (url is null)
                {
                    continue;
                }

                urls.Add(shortUrlService.Get(url.ShortId), new UrlStats(url, shortUrlService));
                idsToQuery.Remove(id);
            }
        }

        if (urls.Count == ids.Count)
        {
            return urls;
        }

        UrlServiceClient client = grpcClientFactory.GetUrlClient(shardId);

        GetUrlsByIdsRequest request = new();
        request.ShortIds.AddRange(idsToQuery);

        GetUrlsByIdsReply reply = await client.GetByIdsAsync(
            request,
            cancellationToken: cancellationToken);

        foreach (GrpcUrlStats? url in reply.Urls.Urls_)
        {
            urls.Add(shortUrlService.Get(url.ShortId), new UrlStats(new DbUrlStats(url), shortUrlService));
        }

        return urls;
    }

    public async Task<Connection<UrlStats>> GetByUserId(
        UserId userId,
        int? first, string? after, int? last, string? before,
        CancellationToken cancellationToken = default)
    {
        string cacheHashKey = CacheUtils.GetUserHashKey(userId);
        string cacheEntryKey = CacheUtils.GetPaginationEntryKey(first, after, last, before);
        Connection<DbUrlStats>? cachedConnection =
            await cacheService.GetHash<Connection<DbUrlStats>>(cacheHashKey, cacheEntryKey);
        if (cachedConnection is not null)
        {
            return UrlUtils.Convert(cachedConnection, shortUrlService);
        }

        UrlServiceClient client = grpcClientFactory.GetUrlClient(userId.ShardId);

        GetUrlsByUserIdRequest request = new() { UserId = userId.Id };
        if (first.HasValue)
        {
            request.First = first.Value;
            if (after is not null)
            {
                request.After = after;
            }
        }
        else if (last.HasValue)
        {
            request.Last = last.Value;
            if (before is not null)
            {
                request.Before = before;
            }
        }

        GetUrlsByUserIdReply reply = await client.GetByUserIdAsync(
            request,
            cancellationToken: cancellationToken);

        PageInfo pageInfo = new()
        {
            HasNextPage = reply.Connection.PageInfo.HasNextPage,
            HasPreviousPage = reply.Connection.PageInfo.HasPreviousPage
        };

        if (reply.Connection.PageInfo.HasStartCursor)
        {
            pageInfo.StartCursor = reply.Connection.PageInfo.StartCursor;
        }

        if (reply.Connection.PageInfo.HasEndCursor)
        {
            pageInfo.EndCursor = reply.Connection.PageInfo.EndCursor;
        }

        List<Edge<UrlStats>> edges = reply.Connection.Edges
            .Select(url => new Edge<UrlStats>
            {
                Cursor = url.Cursor, Node = new UrlStats(new DbUrlStats(url.Node), shortUrlService)
            })
            .ToList();

        Connection<UrlStats> connection = new() { PageInfo = pageInfo, Edges = edges };

        return connection;
    }

    public async Task<bool> Update(
        ShortId id, UserId userId, string destinationUrl,
        CancellationToken cancellationToken = default)
    {
        if (id.Range != userId.ShardId)
        {
            throw RpcExceptionUtils.PermissionDenied(
                new Dictionary<string, string>
                {
                    { nameof(id.Range), id.Range.ToString() }, { nameof(userId.ShardId), userId.ShardId.ToString() }
                },
                $"Shard id mismatch: {id.Range} != {userId.ShardId}");
        }

        if (!destinationUrl.StartsWith("http://") && !destinationUrl.StartsWith("https://"))
        {
            destinationUrl = $"https://{destinationUrl}";
        }

        UrlServiceClient client = grpcClientFactory.GetUrlClient(id.Range);

        UpdateUrlRequest request = new()
        {
            UserId = userId.Id,
            Url = new GrpcUrlMapping { ShortId = id.ToString(), DestinationUrl = destinationUrl }
        };
        UpdateUrlReply reply = await client.UpdateAsync(request, cancellationToken: cancellationToken);

        return reply.Success;
    }

    public async Task<bool> Delete(ShortId id, UserId userId, CancellationToken cancellationToken = default)
    {
        if (id.Range != userId.ShardId)
        {
            throw RpcExceptionUtils.PermissionDenied(
                new Dictionary<string, string>
                {
                    { nameof(id.Range), id.Range.ToString() }, { nameof(userId.ShardId), userId.ShardId.ToString() }
                },
                "Shard id mismatch");
        }

        UrlServiceClient client = grpcClientFactory.GetUrlClient(id.Range);

        DeleteUrlRequest request = new() { UserId = userId.Id, ShortId = id.ToString() };
        DeleteUrlReply reply = await client.DeleteAsync(request, cancellationToken: cancellationToken);

        return reply.Success;
    }
}
