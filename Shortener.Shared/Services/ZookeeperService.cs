using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using AsyncEvent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using org.apache.utils;
using org.apache.zookeeper;
using org.apache.zookeeper.recipes.leader;
using Rabbit.Zookeeper;
using Rabbit.Zookeeper.Implementation;
using Shortener.Shared.Exceptions;
using Shortener.Shared.Utils;
using ChildrenHandler = AsyncEvent.AsyncEventHandler<Shortener.Shared.Services.ChildrenChangedEventArgs>;
using ConnectionHandler = AsyncEvent.AsyncEventHandler<Shortener.Shared.Services.ConnectionStateChangedEventArgs>;

namespace Shortener.Shared.Services;

public enum ShardType
{
    Uninitialized,
    ReadOnly,
    ReadWrite,
    WriteUrlsOnly
}

public sealed record ChildNode(string Name, long ShardId, ShardType ShardType);

public sealed class ChildrenChangedEventArgs : EventArgs
{
    public required IList<ChildNode> ChildNodes { get; init; } = [];
}

public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    public required bool IsConnected { get; init; }
}

public interface IZookeeperService
{
    bool IsConnected { get; }

    ZookeeperClient Client { get; }

    Task<bool> CreatePersistent(string path, byte[] data);

    string GetEphemeralPath(string parentPath, long shardId, string nodeName, ShardType shardState);

    Task<string> CreateChildEphemeral(string ephemeralPath, byte[] data, bool isSequential);

    Task<IList<ChildNode>> GetChildren(string parentPath);

    Task<bool> Delete(string path);

    Task<LeaderElectionSupport> CreateLeaderElection(string parentPath, string hostName);

    Task OnConnectionStateChanged(ConnectionHandler handler);

    void RemoveConnectionStateChangedHandler(ConnectionHandler handler);

    Task OnChildrenChanged(string parentPath, ChildrenHandler handler);

    void RemoveChildrenChangedHandler(string parentPath, ChildrenHandler handler);
}

public sealed class ZookeeperService : IZookeeperService, IDisposable
{
    private readonly Dictionary<string, ChildrenChangedHandlerWrapper> _childrenHandlers = [];
    private readonly SemaphoreSlim _childrenLock = new(1, 1);
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConnectionStateChangeHandler _zkConnectionHandler;

    public ZookeeperService(IConfiguration configuration, TelemetryBase telemetry, ILoggerFactory loggerFactory)
    {
        _zkConnectionHandler = async (_, args) =>
        {
            bool connected = args.State == Watcher.Event.KeeperState.SyncConnected;
            if (ConnectionHandler is not null)
            {
                await ConnectionHandler.InvokeAsync(
                    this,
                    new ConnectionStateChangedEventArgs { IsConnected = connected });
            }
        };

        if (ZooKeeper.CustomLogConsumer is null)
        {
            ILogConsumer logger = new ZookeeperLogger(telemetry, loggerFactory);
            ZooKeeper.LogToFile = false;
            ZooKeeper.LogToTrace = false;
            ZooKeeper.CustomLogConsumer = logger;
        }

        string connectionString = ConnectionStringUtils.GetZookeeper(configuration);
        Client = new ZookeeperClient(connectionString);

        Client.SubscribeStatusChange(_zkConnectionHandler);
    }

    public void Dispose()
    {
        Client.UnSubscribeStatusChange(_zkConnectionHandler);
        Client.Dispose();
        _childrenLock.Dispose();
        _connectionLock.Dispose();
    }

    public bool IsConnected => Client.ZooKeeper.getState() == ZooKeeper.States.CONNECTED;

    public ZookeeperClient Client { get; }

    public async Task<bool> CreatePersistent(string path, byte[] data)
    {
        try
        {
            await Client.CreatePersistentAsync(path, data);
            return true;
        }
        catch (KeeperException.NodeExistsException)
        {
            return false;
        }
    }

    public string GetEphemeralPath(string parentPath, long shardId, string nodeName, ShardType shardState)
        => $"{parentPath}/{shardId}_{nodeName}_{shardState}_";

    public async Task<string> CreateChildEphemeral(string ephemeralPath, byte[] data, bool isSequential) =>
        await Client.CreateEphemeralAsync(ephemeralPath, data, isSequential);

    public async Task<IList<ChildNode>> GetChildren(string parentPath)
    {
        List<ChildNode> nodes = [];
        IEnumerable<string>? zChildren = await Client.GetChildrenAsync(parentPath);

        nodes.AddRange(zChildren.Select(ParseChildNode));

        return nodes;
    }

    public async Task<bool> Delete(string path)
    {
        try
        {
            await Client.DeleteAsync(path);
            return true;
        }
        catch (KeeperException.NoNodeException)
        {
            return false;
        }
    }

    public async Task<LeaderElectionSupport> CreateLeaderElection(string parentPath, string hostName)
    {
        await CreatePersistent(parentPath, []);

        LeaderElectionSupport leaderElection = new(Client.ZooKeeper, parentPath, hostName);
        return leaderElection;
    }

    public async Task OnConnectionStateChanged(ConnectionHandler handler)
    {
        try
        {
            await _connectionLock.WaitAsync();
            ConnectionHandler += handler;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public void RemoveConnectionStateChangedHandler(ConnectionHandler handler)
    {
        try
        {
            _connectionLock.Wait();
            ConnectionHandler -= handler;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task OnChildrenChanged(string parentPath, ChildrenHandler handler)
    {
        try
        {
            await _childrenLock.WaitAsync();

            if (!_childrenHandlers.TryGetValue(parentPath, out ChildrenChangedHandlerWrapper? existingWrapper))
            {
                ChildrenChangedHandlerWrapper newWrapper = new() { ParentPath = parentPath };
                newWrapper.Handlers += handler;
                _childrenHandlers.Add(parentPath, newWrapper);

                await Client.SubscribeChildrenChange(parentPath, newWrapper.ZkHandler);
            }
            else
            {
                existingWrapper.Handlers += handler;
            }
        }
        finally
        {
            _childrenLock.Release();
        }
    }

    public void RemoveChildrenChangedHandler(string parentPath, ChildrenHandler handler)
    {
        try
        {
            _childrenLock.Wait();

            if (!_childrenHandlers.TryGetValue(parentPath, out ChildrenChangedHandlerWrapper? existingWrapper))
            {
                return;
            }

            existingWrapper.Handlers -= handler;
            if (!existingWrapper.IsNull())
            {
                return;
            }

            _childrenHandlers.Remove(parentPath);
            Client.UnSubscribeChildrenChange(parentPath, existingWrapper.ZkHandler);
        }
        finally
        {
            _childrenLock.Release();
        }
    }

    private event ConnectionHandler? ConnectionHandler;

    private static ChildNode ParseChildNode(string childName)
    {
        string[] data = childName.Split('_');
        if (data.Length != 4)
        {
            throw new ZookeeperErrorException($"Invalid node name: {childName}");
        }

        return new ChildNode(data[1], long.Parse(data[0]), Enum.Parse<ShardType>(data[2]));
    }

    private sealed class ChildrenChangedHandlerWrapper
    {
        public ChildrenChangedHandlerWrapper() =>
            ZkHandler = async (_, args) =>
            {
                List<ChildNode> nodes = [];
                nodes.AddRange(args.CurrentChildrens.Select(ParseChildNode));

                if (Handlers is not null)
                {
                    await Handlers.InvokeAsync(this, new ChildrenChangedEventArgs { ChildNodes = nodes });
                }
            };

        public required string ParentPath { get; init; }

        public NodeChildrenChangeHandler ZkHandler { get; }

        public bool IsNull() => Handlers is null;

        public event ChildrenHandler? Handlers;
    }
}

// https://github.com/shayhatsor/zookeeper/blob/trunk/src/csharp/src/ZooKeeperNetEx/utils/log/LogWriter.cs
public sealed class ZookeeperLogger(TelemetryBase telemetry, ILoggerFactory loggerFactory) : ILogConsumer
{
    private static readonly string[] s_traceTable = ["OFF", "ERROR", "WARNING", "INFO", "VERBOSE"];
    private readonly ILogger<ZookeeperLogger> _logger = loggerFactory.CreateLogger<ZookeeperLogger>();

    public void Log(TraceLevel severity, string className, string message, Exception exception)
    {
        string exc = PrintException(exception);
        string msg = $"[{PrintDate()} \t{s_traceTable[(int)severity]} \t{className} \t{message}] \t{exc}";

        switch (severity)
        {
            case TraceLevel.Off:
                break;
            case TraceLevel.Error:
                telemetry.AddZookeeperErrorCount();
                _logger.LogError("{Message}", msg);
                break;
            case TraceLevel.Warning:
                _logger.LogWarning("{Message}", msg);
                break;
            case TraceLevel.Info:
                _logger.LogInformation("{Message}", msg);
                break;
            case TraceLevel.Verbose:
                _logger.LogDebug("{Message}", msg);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(severity));
        }
    }

    private static string PrintDate()
    {
        // http://www.csharp-examples.net/string-format-datetime/
        // http://msdn.microsoft.com/en-us/library/system.globalization.datetimeformatinfo.aspx
        const string timeFormat = "HH:mm:ss.fff 'GMT'"; // Example: 09:50:43.341 GMT
        const string dateFormat = "yyyy-MM-dd " + timeFormat;
        // Example: 2010-09-02 09:50:43.341 GMT - Variant of UniversalSorta�bleDateTimePat�tern
        return DateTime.UtcNow.ToString(dateFormat);
    }

    private static string PrintException(Exception? exception) =>
        exception == null ? string.Empty : PrintException_Helper(exception, 0, true);

    private static string PrintException_Helper(Exception? exception, int level, bool includeStackTrace)
    {
        if (exception == null)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        sb.Append(PrintOneException(exception, level, includeStackTrace));

        if (exception is AggregateException aggregateException)
        {
            ReadOnlyCollection<Exception>? innerExceptions = aggregateException.InnerExceptions;
            if (innerExceptions == null)
            {
                return sb.ToString();
            }

            foreach (Exception inner in innerExceptions)
            {
                // call recursively on all inner exceptions. Same level for all.
                sb.Append(PrintException_Helper(inner, level + 1, includeStackTrace));
            }
        }
        else if (exception.InnerException != null)
        {
            // call recursively on a single inner exception.
            sb.Append(PrintException_Helper(exception.InnerException, level + 1, includeStackTrace));
        }

        return sb.ToString();
    }

    private static string PrintOneException(Exception? exception, int level, bool includeStackTrace)
    {
        if (exception == null)
        {
            return string.Empty;
        }

        string stack = string.Empty;
        if (includeStackTrace && exception.StackTrace != null)
        {
            stack = $"{Environment.NewLine}{exception.StackTrace}";
        }

        string message = exception.Message;

        return $"{Environment.NewLine}Exc level {level}: {exception.GetType()}: {message}{stack}";
    }
}
