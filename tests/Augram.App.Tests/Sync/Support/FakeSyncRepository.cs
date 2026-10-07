using Augram.Core.Abstractions;

namespace Augram.App.Tests.Sync.Support;

/// <summary>
/// An in-memory <see cref="ISyncRepository"/> over a <see cref="SyncRemote"/>: no git, no network. Failures are
/// switchable, every call is counted, <see cref="HoldPull"/> blocks the next pulls until released (a run in progress),
/// and <see cref="MaxConcurrent"/> proves no two runs overlap.
/// </summary>
internal sealed class FakeSyncRepository : ISyncRepository
{
    private readonly SyncRemote _remote;
    private readonly object _gate = new();
    private readonly List<string> _prepared = [];
    private ManualResetEventSlim? _hold;
    private int _inside;
    private int _maxConcurrent;
    private int _pulls;

    public FakeSyncRepository(SyncRemote remote, string folder = "memory://clone")
    {
        _remote = remote;
        Folder = folder;
    }

    public string Folder { get; }

    /// <summary>When set, Prepare and Pull fail with this line.</summary>
    public string? FailWith { get; set; }

    public IReadOnlyList<string> PreparedUrls
    {
        get
        {
            lock (_gate)
            {
                return [.. _prepared];
            }
        }
    }

    public int Pulls => Volatile.Read(ref _pulls);

    public int MaxConcurrent => Volatile.Read(ref _maxConcurrent);

    /// <summary>Pulls from now on wait until <see cref="Release"/>.</summary>
    public void HoldPull()
    {
        lock (_gate)
        {
            _hold = new ManualResetEventSlim(false);
        }
    }

    public void Release()
    {
        ManualResetEventSlim? hold;
        lock (_gate)
        {
            hold = _hold;
            _hold = null;
        }

        hold?.Set();
    }

    public SyncOperationResult Prepare(string repositoryUrl)
    {
        lock (_gate)
        {
            _prepared.Add(repositoryUrl);
        }

        return Result();
    }

    public SyncOperationResult Pull()
    {
        var inside = Interlocked.Increment(ref _inside);
        try
        {
            InterlockedMax(ref _maxConcurrent, inside);
            Interlocked.Increment(ref _pulls);
            ManualResetEventSlim? hold;
            lock (_gate)
            {
                hold = _hold;
            }

            hold?.Wait(TimeSpan.FromSeconds(10));
            return Result();
        }
        finally
        {
            Interlocked.Decrement(ref _inside);
        }
    }

    public IReadOnlyDictionary<string, string> ReadMachineFiles() => _remote.Files;

    public SyncOperationResult Publish(string machineId, string content, string message)
    {
        _remote.Write(machineId, content);
        return SyncOperationResult.Ok;
    }

    private SyncOperationResult Result() => FailWith is { } error ? SyncOperationResult.Failed(error) : SyncOperationResult.Ok;

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }
}
