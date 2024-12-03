using Grpc.Core;
using Shortener.Admin.Data;
using Shortener.Admin.Repositories;
using Shortener.BackendShared.Utils;
using Shortener.Shared.Grpc;
using Shortener.Shared.Services;
using AdminServiceBase = Shortener.Shared.Grpc.AdminService.AdminServiceBase;

namespace Shortener.Admin.Services;

public sealed class AdminService(
    IBackgroundTaskQueue taskQueue,
    IBannedDomainRepository bannedDomainRepository)
    : AdminServiceBase
{
    public override async Task<IsBannedReply> IsBanned(IsBannedRequest request, ServerCallContext context)
    {
        BannedDomain? bannedDomain = await bannedDomainRepository.Get(request.Domain);
        bool banned = bannedDomain is not null;

        await UpdateDomainInBackground(request.Domain, banned);

        return new IsBannedReply { Banned = banned };
    }

    private async Task UpdateDomainInBackground(string domain, bool banned)
    {
        string key = CacheUtils.GetBannedDomainKey(domain);
        Func<IServiceScopeFactory, CancellationToken, ValueTask> task =
            CacheUtils.CreateSetTask(key, banned, TimeSpan.FromMinutes(10));

        await taskQueue.QueueBackgroundWorkItem(task);
    }
}
