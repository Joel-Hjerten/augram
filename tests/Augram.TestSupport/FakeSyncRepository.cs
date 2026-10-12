using Augram.Core.Abstractions;

namespace Augram.TestSupport;

/// <summary>
/// An in-memory <see cref="ISyncRepository"/> over a <see cref="SyncRemote"/>: no git, no network. Failures are switchable
/// (<see cref="FailWith"/>, <see cref="FailPublishWith"/>), every prepare and pull is counted, <see cref="HoldPull"/> blocks
/// the next pulls until released (a run in progress), and <see cref="MaxConcurrent"/> proves no two runs overlap.
/// </summary>
public sealed class FakeSyncRepository : ISyncRepository
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

    /// <summary>When set, Publish fails with this line and writes nothing.</summary>
    public string? FailPublishWith { get; set; }

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

        return Result(FailWith);
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
            return Result(FailWith);
        }
        finally
        {
            Interlocked.Decrement(ref _inside);
        }
    }

    public IReadOnlyDictionary<string, string> ReadMachineFiles() => new Dictionary<string, string>(_remote.Files, StringComparer.OrdinalIgnoreCase);

    public SyncOperationResult Publish(string machineId, string content, string message)
    {
        if (FailPublishWith is { } error)
        {
            return SyncOperationResult.Failed(error);
        }

        _remote.Write(machineId, content, message);
        return SyncOperationResult.Ok;
    }

    private static SyncOperationResult Result(string? error) => error is null ? SyncOperationResult.Ok : SyncOperationResult.Failed(error);

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen)
        {
        }
    }
}
