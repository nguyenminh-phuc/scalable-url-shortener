using AsyncEvent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using org.apache.zookeeper.recipes.leader;
using Shortener.Shared;
using Shortener.Shared.Services;

namespace Shortener.BackendShared.Services;

public interface IElectionService
{
    bool IsHealthy { get; }

    Task<bool> IsMaster();

    Task Initialize();
}

public abstract class ElectionServiceBase : IElectionService, INotifyElectionEventChanged, IAsyncDisposable
{
    private readonly AsyncEventHandler<ConnectionStateChangedEventArgs> _connectionHandler;
    private readonly string _electionChildPath;
    private readonly string _electionHostName;
    private readonly LeaderElectionListener _electionListener;
    private readonly string _electionParentPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IZookeeperService _zookeeperService;
    private ElectionEventType _electionType = ElectionEventType.FAILED;
    private bool _initialized;
    private LeaderElectionSupport? _leaderElection;

    protected ElectionServiceBase(
        IConfiguration configuration,
        string electionChildPath,
        TelemetryBase telemetry,
        ILoggerFactory loggerFactory,
        IZookeeperService zookeeperService)
    {
        _zookeeperService = zookeeperService;
        _electionParentPath = configuration["ZOOKEEPER_ELECTION_PATH"] ?? Constants.ElectionPath;
        _electionChildPath = $"{_electionParentPath}/{electionChildPath}";
        _electionHostName = telemetry.ServiceInstanceId;
        _electionListener = new LeaderElectionListener(loggerFactory, this);
        _connectionHandler = async (_, e) => await HandleDisconnected(e.IsConnected);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    public bool IsHealthy => _electionType is ElectionEventType.ELECTED_COMPLETE or ElectionEventType.READY_COMPLETE;

    public async Task<bool> IsMaster()
    {
        if (_leaderElection is null)
        {
            return false;
        }

        string? leaderHostName = await _leaderElection.getLeaderHostName();
        return _electionHostName == leaderHostName;
    }

    public async Task Initialize()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            await _lock.WaitAsync();

            await _zookeeperService.CreatePersistent(_electionParentPath, []);
            await _zookeeperService.OnConnectionStateChanged(_connectionHandler);

            await StartElection();

            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task OnEventChanged(ElectionEventType eventType)
    {
        _electionType = eventType;
        return Task.CompletedTask;
    }

    private async Task HandleDisconnected(bool isConnected)
    {
        try
        {
            await _lock.WaitAsync();

            if (!isConnected)
            {
                await StopElection();
            }
            else
            {
                await StartElection();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await StopElection();
        _lock.Dispose();
    }

    private async Task StartElection()
    {
        if (_leaderElection is not null)
        {
            return;
        }

        _leaderElection = await _zookeeperService.CreateLeaderElection(_electionChildPath, _electionHostName);
        _leaderElection.addListener(_electionListener);
        await _leaderElection.start();
    }

    private async Task StopElection()
    {
        if (_leaderElection is null)
        {
            return;
        }

        await _leaderElection.stop();
        _leaderElection.removeListener(_electionListener);
        _leaderElection = null;
    }
}

public interface INotifyElectionEventChanged
{
    Task OnEventChanged(ElectionEventType eventType);
}

public sealed class LeaderElectionListener(ILoggerFactory loggerFactory, INotifyElectionEventChanged notifier) :
    LeaderElectionAware
{
    private readonly ILogger<LeaderElectionListener> _logger = loggerFactory.CreateLogger<LeaderElectionListener>();

    public async Task onElectionEvent(ElectionEventType eventType)
    {
        _logger.LogInformation("OnElectionEvent: {EventType}", eventType);
        await notifier.OnEventChanged(eventType);
    }
}
