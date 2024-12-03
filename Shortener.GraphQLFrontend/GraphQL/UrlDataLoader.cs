using Shortener.FrontendShared.Middleware;
using Shortener.FrontendShared.Services;
using Shortener.Shared.Entities;
using Shortener.Shared.Utils;
using UrlStats = Shortener.FrontendShared.Dtos.UrlStats;

namespace Shortener.GraphQLFrontend.GraphQL;

public sealed class UrlDataLoader(
    IHttpContextAccessor context,
    IUrlService urlService,
    IBatchScheduler batchScheduler,
    DataLoaderOptions? options = null)
    : BatchDataLoader<string, UrlStats>(batchScheduler, options)
{
    protected override async Task<IReadOnlyDictionary<string, UrlStats>> LoadBatchAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        UserId userId = (UserId)context.HttpContext!.Items[JwtHandler.UserId]!;
        if (keys.Select(ShortIdUtils.ParseUrl).Any(shortId => shortId.Range != userId.ShardId))
        {
            throw new GraphQLException("Forbidden");
        }

        Dictionary<string, UrlStats> dict = [];

        if (keys.Count == 1)
        {
            string shortUrl = keys.Single();
            ShortId shortId = ShortIdUtils.ParseUrl(shortUrl);

            UrlStats url = await urlService.GetById(shortId, cancellationToken);
            dict.Add(shortUrl, url);

            return dict;
        }

        List<ShortId> ids = keys.Select(ShortIdUtils.ParseUrl).ToList();

        IDictionary<string, UrlStats> urls = await urlService.GetByIds(ids, cancellationToken);
        foreach ((string shortUrl, UrlStats url) in urls)
        {
            dict.Add(shortUrl, url);
        }

        return dict;
    }
}
