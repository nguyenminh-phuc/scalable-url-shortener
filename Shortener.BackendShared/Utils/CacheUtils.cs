using Microsoft.Extensions.DependencyInjection;
using Shortener.BackendShared.Services;

namespace Shortener.BackendShared.Utils;

public static class CacheUtils
{
    public static string GetBannedDomainKey(string name) => $"Domain:{name}";

    public static Func<IServiceScopeFactory, CancellationToken, ValueTask> CreateSetTask<TData>(
        string key, TData data,
        TimeSpan duration)
    {
        return Task;

        async ValueTask Task(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
            ICacheService service = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await service.Set(key, data, duration);
        }
    }

    public static Func<IServiceScopeFactory, CancellationToken, ValueTask> CreateHashSetTask<TData>(
        string hashKey, string entryKey, TData data,
        TimeSpan duration)
    {
        return Task;

        async ValueTask Task(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
            ICacheService service = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await service.SetHash(hashKey, entryKey, data, duration);
        }
    }

    public static Func<IServiceScopeFactory, CancellationToken, ValueTask> CreateSetStringTask(
        string key, string data,
        TimeSpan duration)
    {
        return Task;

        async ValueTask Task(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
            ICacheService service = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await service.SetString(key, data, duration);
        }
    }

    public static Func<IServiceScopeFactory, CancellationToken, ValueTask> CreateRemoveTask(string key)
    {
        return Task;

        async ValueTask Task(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
            ICacheService service = scope.ServiceProvider.GetRequiredService<ICacheService>();
            await service.Remove(key);
        }
    }
}
