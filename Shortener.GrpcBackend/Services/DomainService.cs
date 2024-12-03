using Shortener.BackendShared.Services;
using Shortener.BackendShared.Utils;
using Shortener.Shared.Grpc;

namespace Shortener.GrpcBackend.Services;

public interface IDomainService
{
    Task<bool> IsBanned(string domain);
}

public sealed class DomainService(AdminService.AdminServiceClient adminServiceClient, ICacheService cacheService)
    : IDomainService
{
    public async Task<bool> IsBanned(string domain)
    {
        string key = CacheUtils.GetBannedDomainKey(domain);
        bool? cachedBanned = await cacheService.GetOrNullable<bool>(key);
        if (cachedBanned.HasValue && cachedBanned.Value)
        {
            return true;
        }

        IsBannedRequest request = new() { Domain = domain };
        IsBannedReply reply = await adminServiceClient.IsBannedAsync(request);

        return reply.Banned;
    }
}
