using MassTransit;
using Shortener.BackendShared.Contracts;
using Shortener.BackendShared.Services;
using Shortener.GrpcBackend.Data;
using Shortener.GrpcBackend.Repositories;
using Shortener.GrpcBackend.Services;
using Shortener.Shared.Utils;

namespace Shortener.GrpcBackend.Consumers;

public sealed class GetUrlCountConsumer(
    ICacheService cacheService,
    IElectionService electionService,
    IShardService shardService,
    IDomainRepository domainRepository,
    IUrlRepository urlRepository)
    : IConsumer<GetUrlCountRequest>
{
    public async Task Consume(ConsumeContext<GetUrlCountRequest> context)
    {
        if (!await electionService.IsMaster())
        {
            return;
        }

        Domain? domain = await domainRepository.Get(context.Message.Domain, context.CancellationToken);

        int count = 0;
        if (domain is not null)
        {
            count = await urlRepository.GetCount(domain.Id, context.CancellationToken);
        }

        await cacheService.SetHash(
            CacheUtils.UrlCountHashKey(context.Message.Domain),
            shardService.Id.ToString(),
            count,
            TimeSpan.FromMinutes(5));
    }
}
