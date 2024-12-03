using AsyncEvent;
using Microsoft.Extensions.Configuration;

namespace Shortener.Shared.Services;

public enum ShardEvent
{
    ShardConnectionStateChanged,
    ShardStatusChanged
}

public abstract class ShardEventArgs(ShardEvent type) : EventArgs
{
    public ShardEvent Type { get; } = type;
}

public sealed class ShardConnectionStateChangedEventArgs() : ShardEventArgs(ShardEvent.ShardConnectionStateChanged)
{
    public bool IsConnected { get; init; }
}

public sealed class ShardStatusChangedEventArgs() : ShardEventArgs(ShardEvent.ShardStatusChanged)
{
    public required HashSet<long> OnlineShards { get; init; }
}

public interface IShardService
{
    IList<long> OnlineShards { get; }

    event AsyncEventHandler<ShardEventArgs>? Handlers;

    Task Initialize();

    bool IsOnline(long shardId);

    long? GetShardIdForNewUser();

    bool CanShardCreateNewUrl(long shardId);
}

public sealed class ShardService : IShardService, IDisposable
{
    private readonly AsyncEventHandler<ChildrenChangedEventArgs> _childrenHandler;
    private readonly AsyncEventHandler<ConnectionStateChangedEventArgs> _connectionHandler;
    private readonly SemaphoreSlim _eventLock = new(1, 1);
    private readonly Random _random = new();
    private readonly ReaderWriterLockSlim _rwLock = new();
    private readonly string _shardPath;
    private readonly IZookeeperService _zookeeperService;
    private bool _initialized;
    private List<long> _onlineShardList = []; // List: O(1) for accessing elements by index
    private HashSet<long> _onlineShardSet = []; // HashSet: O(1) for lookups
    private HashSet<long> _shardIdsForNewUrls = [];
    private List<long> _shardIdsForNewUsers = [];

    public ShardService(IConfiguration configuration, IZookeeperService zookeeperService)
    {
        _shardPath = configuration["ZOOKEEPER_SHARD_PATH"] ?? Constants.ShardPath;
        _zookeeperService = zookeeperService;
        _connectionHandler = async (_, e) =>
        {
            ShardConnectionStateChangedEventArgs eventArgs = new() { IsConnected = e.IsConnected };
            if (Handlers is not null)
            {
                await Handlers.InvokeAsync(this, eventArgs);
            }
        };
        _childrenHandler = async (_, e) => { await UpdateShards(e.ChildNodes); };
    }

    public void Dispose()
    {
        if (_initialized)
        {
            _zookeeperService.RemoveConnectionStateChangedHandler(_connectionHandler);
            _zookeeperService.RemoveChildrenChangedHandler(_shardPath, _childrenHandler);
        }

        _rwLock.Dispose();
        _eventLock.Dispose();
    }

    public event AsyncEventHandler<ShardEventArgs>? Handlers;

    public async Task Initialize()
    {
        await _zookeeperService.OnConnectionStateChanged(_connectionHandler);
        await _zookeeperService.OnChildrenChanged(_shardPath, _childrenHandler);

        await _zookeeperService.CreatePersistent(_shardPath, []);

        IList<ChildNode> children = await _zookeeperService.GetChildren(_shardPath);
        await UpdateShards(children);

        _initialized = true;
    }

    public IList<long> OnlineShards
    {
        get
        {
            try
            {
                _rwLock.EnterReadLock();
                return _onlineShardList;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }
    }

    public bool IsOnline(long shardId)
    {
        try
        {
            _rwLock.EnterReadLock();
            return _onlineShardSet.Contains(shardId);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public long? GetShardIdForNewUser()
    {
        try
        {
            _rwLock.EnterReadLock();

            if (_shardIdsForNewUsers.Count == 0)
            {
                return null;
            }

            int index = _random.Next(_shardIdsForNewUsers.Count);
            return _shardIdsForNewUsers[index];
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public bool CanShardCreateNewUrl(long shardId)
    {
        try
        {
            _rwLock.EnterReadLock();
            return _shardIdsForNewUrls.Contains(shardId);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    private async Task UpdateShards(IList<ChildNode> children)
    {
        ShardStatusChangedEventArgs e;
        try
        {
            _rwLock.EnterWriteLock();

            _onlineShardSet = [];
            _onlineShardList = [];
            _shardIdsForNewUsers = [];
            _shardIdsForNewUrls = [];
            HashSet<long> readWriteShards = [];
            HashSet<long> writeUrlsOnlyShards = [];
            HashSet<long> readOnlyShards = [];

            foreach (ChildNode child in children)
            {
                _onlineShardSet.Add(child.ShardId);

                switch (child.ShardType)
                {
                    case ShardType.ReadWrite:
                        readWriteShards.Add(child.ShardId);
                        break;
                    case ShardType.WriteUrlsOnly:
                        writeUrlsOnlyShards.Add(child.ShardId);
                        break;
                    case ShardType.ReadOnly:
                        readOnlyShards.Add(child.ShardId);
                        break;
                    case ShardType.Uninitialized:
                    default:
                        break;
                }
            }

            foreach (long shardId in readWriteShards.Where(shardId => !readOnlyShards.Contains(shardId)))
            {
                if (!writeUrlsOnlyShards.Contains(shardId) && !_shardIdsForNewUsers.Contains(shardId))
                {
                    _shardIdsForNewUsers.Add(shardId);
                }

                _shardIdsForNewUrls.Add(shardId);
            }

            foreach (long shardId in writeUrlsOnlyShards.Where(shardId => !readOnlyShards.Contains(shardId)))
            {
                _shardIdsForNewUrls.Add(shardId);
            }

            foreach (long shardId in _onlineShardSet)
            {
                _onlineShardList.Add(shardId);
            }

            e = new ShardStatusChangedEventArgs { OnlineShards = _onlineShardSet };
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }

        try
        {
            await _eventLock.WaitAsync();
            if (Handlers is not null)
            {
                await Handlers.InvokeAsync(this, e);
            }
        }
        finally
        {
            _eventLock.Release();
        }
    }
}
