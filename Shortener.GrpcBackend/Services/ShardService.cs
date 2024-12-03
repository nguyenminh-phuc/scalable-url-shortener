using org.apache.zookeeper;
using Shortener.GrpcBackend.Repositories;
using Shortener.Shared;
using Shortener.Shared.Exceptions;
using Shortener.Shared.Services;
using Range = Shortener.GrpcBackend.Data.Range;

namespace Shortener.GrpcBackend.Services;

public interface IShardService
{
    long Id { get; }

    ShardType Type { get; }

    Task Initialize(IRangeRepository rangeRepository, IUrlRepository urlRepository);

    Task ChangeType(ShardType newType);
}

public sealed class ShardService : IShardService, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _nodeName;
    private readonly string _shardParentPath;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IZookeeperService _zookeeperService;
    private bool _initialized;
    private string? _shardChildPath;
    private volatile ShardType _type;

    public ShardService(
        IConfiguration configuration,
        IBackgroundTaskQueue taskQueue,
        IZookeeperService zookeeperService)
    {
        if (!long.TryParse(configuration["SHARD_ID"], out long id))
        {
            throw new Exception("SHARD_ID is required");
        }

        Id = id;
        _taskQueue = taskQueue;
        _zookeeperService = zookeeperService;
        _shardParentPath = configuration["ZOOKEEPER_SHARD_PATH"] ?? Constants.ShardPath;
        _nodeName = $"backend{Id}";
    }

    public void Dispose() => _lock.Dispose();

    public long Id { get; }

    public ShardType Type => _type;

    public async Task Initialize(IRangeRepository rangeRepository, IUrlRepository urlRepository)
    {
        if (_initialized)
        {
            return;
        }

        Range range = await rangeRepository.Get();
        if (range.RangeId != Id)
        {
            throw new DatabaseErrorException($"Shard id mismatch: {range.RangeId} != {Id}");
        }

        int? maxId = await urlRepository.GetMaxId();
        if (maxId is null || maxId < Constants.ThresholdForNewUsers)
        {
            _type = ShardType.ReadWrite;
        }
        else if (maxId < Constants.ThresholdForNewUrls)
        {
            _type = ShardType.WriteUrlsOnly;
        }
        else
        {
            _type = ShardType.ReadOnly;
        }

        await _zookeeperService.CreatePersistent(_shardParentPath, []);

        string tmpEphemeralPath = _zookeeperService.GetEphemeralPath(_shardParentPath, Id, _nodeName, _type);
        _shardChildPath = await _zookeeperService.CreateChildEphemeral(tmpEphemeralPath, [], true);

        _initialized = true;
    }

    public async Task ChangeType(ShardType newType)
    {
        if (_type == newType)
        {
            return;
        }

        try
        {
            await _lock.WaitAsync();

            if (_type == newType)
            {
                return;
            }

            string oldEphemeralPath = _shardChildPath!;
            string newEphemeralPath = _zookeeperService.GetEphemeralPath(_shardParentPath, Id, _nodeName, newType);

            _shardChildPath = await _zookeeperService.CreateChildEphemeral(newEphemeralPath, [], true);
            await DeleteOldEphemeralPathInBackground(oldEphemeralPath);

            _type = newType;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task DeleteOldEphemeralPathInBackground(string oldEphemeralPath)
    {
        await _taskQueue.QueueBackgroundWorkItem(DeleteTask);
        return;

        async ValueTask DeleteTask(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _zookeeperService.Delete(oldEphemeralPath);
            }
            catch (KeeperException)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                await _taskQueue.QueueBackgroundWorkItem(DeleteTask);
            }
        }
    }
}
