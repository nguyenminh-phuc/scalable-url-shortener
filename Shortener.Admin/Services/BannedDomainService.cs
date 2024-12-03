using Shortener.Admin.Repositories;
using Shortener.Shared.Services;
using CacheUtils = Shortener.BackendShared.Utils.CacheUtils;

namespace Shortener.Admin.Services;

public interface IBannedDomainService
{
    Task<int> GetCount(CancellationToken cancellationToken);

    Task<bool> Ban(string name);

    Task<bool> Unban(string name);
}

public sealed class BannedDomainService(IBackgroundTaskQueue taskQueue, IBannedDomainRepository bannedDomainRepository)
    :
        IBannedDomainService
{
    public async Task<int> GetCount(CancellationToken cancellationToken) =>
        await bannedDomainRepository.GetCount(cancellationToken);

    public async Task<bool> Ban(string name)
    {
        bool added = await bannedDomainRepository.Add(name);
        await RemoveDomainInBackground(name);

        return added;
    }

    public async Task<bool> Unban(string name)
    {
        bool deleted = await bannedDomainRepository.Delete(name);
        await RemoveDomainInBackground(name);

        return deleted;
    }

    private async Task RemoveDomainInBackground(string name)
    {
        string key = CacheUtils.GetBannedDomainKey(name);
        Func<IServiceScopeFactory, CancellationToken, ValueTask> task = CacheUtils.CreateRemoveTask(key);

        await taskQueue.QueueBackgroundWorkItem(task);
    }
}
