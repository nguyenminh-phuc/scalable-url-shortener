using MassTransit;
using Shortener.Admin.Dtos;
using Shortener.BackendShared.Contracts;
using Shortener.BackendShared.Services;
using Shortener.Shared.Services;
using Shortener.Shared.Utils;

namespace Shortener.Admin.Services;

public interface IUrlService
{
    Task MakeCountAggregation(string domain);

    Task<UrlCounts> GetAggregatedCounts(string domain);
}

public sealed class UrlService(IBus bus, ICacheService cacheService, IShardService shardService) : IUrlService
{
    public async Task MakeCountAggregation(string name)
    {
        GetUrlCountRequest request = new(name);

        await bus.Publish(request);
    }

    public async Task<UrlCounts> GetAggregatedCounts(string domain)
    {
        UrlCounts result = new() { Domain = domain };
        foreach (long shard in shardService.OnlineShards)
        {
            result.Counts.Add(shard, null);
        }

        string hashKey = CacheUtils.UrlCountHashKey(domain);
        IDictionary<string, int>? entries = await cacheService.GetHashAll<int>(hashKey);
        if (entries is null)
        {
            return result;
        }

        foreach ((string shardId, int count) in entries)
        {
            result.Counts[long.Parse(shardId)] = count;
        }

        return result;
    }
}
