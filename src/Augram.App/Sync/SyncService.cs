using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// Machine-to-machine sync (F8 sync) as the rest of the App sees it: the user's requests, the status and the one
/// <see cref="Changed"/> event, over two parts it owns. <see cref="SyncWorker"/> runs the jobs on one background thread
/// (never two runs at once; requests coalesce) and keeps the last report; <see cref="SyncScheduler"/> decides when to
/// ask it (the start-up run, the 20 s change delay, the 5 minute poll, automatic sync switched on, a new URL) and
/// pauses automatic runs after a join question or a newer Augram. The user's own triggers (<see cref="SyncNow"/>,
/// <see cref="Join"/>, <see cref="Resolve"/>) go straight to the worker and always run while a repository is set. Both
/// parts share the worker's lock, and <see cref="Snapshot"/> reads what the status lines need under it at once.
/// <see cref="Changed"/> is raised on the worker when a run starts and when it ends; consumers marshal.
/// <see cref="Dispose"/> cancels the timers and stops the worker in one step, then stops the store thread (nothing is
/// applied after it), stops listening and waits briefly for the worker: a run inside a git call is abandoned on its
/// background thread, which applies and saves nothing more. Under <c>--no-engine</c> sync works the same: it never
/// touches input.
/// </summary>
public sealed class SyncService : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly SyncCoordinator _coordinator;
    private readonly SyncStoreThread _storeThread;
    private readonly SyncWorker _worker;
    private readonly SyncScheduler _scheduler;
    private bool _disposed;

    /// <remarks><c>schedule</c> runs the action once after the delay on any thread, and disposing its result cancels it; null is a real timer per call, tests pass a manual one.</remarks>
    public SyncService(
        SyncCoordinator coordinator,
        SettingsStore settings,
        GestureLibrary gestures,
        MappingStore mapping,
        SyncBaseStore bases,
        SyncStoreThread storeThread,
        IClock clock,
        IEventLog log,
        Func<TimeSpan, Action, IDisposable>? schedule = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _storeThread = storeThread ?? throw new ArgumentNullException(nameof(storeThread));
        _worker = new SyncWorker(coordinator, bases, clock, log);
        _scheduler = new SyncScheduler(_worker, settings, gestures, mapping, storeThread, schedule);
    }

    /// <summary>Raised on the worker when a run starts and when it ends; handlers marshal to the UI thread themselves and return at once.</summary>
    public event EventHandler? Changed
    {
        add => _worker.Changed += value;
        remove => _worker.Changed -= value;
    }

    /// <summary>True when a repository is set.</summary>
    public bool IsConfigured => _settings.Current.Sync.IsOn;

    public bool IsRunning => _worker.IsRunning;

    /// <inheritdoc cref="SyncScheduler.IsPaused"/>
    public bool IsPaused => _scheduler.IsPaused;

    /// <summary>The last run's report; null before the first run.</summary>
    public SyncReport? LastReport => _worker.LastReport;

    /// <summary>What started the last run; null before the first run.</summary>
    public SyncTrigger? LastTrigger => _worker.LastTrigger;

    /// <summary>The names of the machines this one has synced with, as of the last run ("from Mac").</summary>
    public IReadOnlyList<string> OtherMachines => _worker.OtherMachines;

    /// <summary>How many runs have finished (tests and diagnostics).</summary>
    public int RunCount => _worker.RunCount;

    /// <summary>Everything the status lines read, with its <see cref="SyncPhase"/>, taken under the sync lock at once. Safe from any thread.</summary>
    public SyncSnapshot Snapshot
    {
        get
        {
            lock (_worker.Gate)
            {
                return SyncSnapshot.Of(IsConfigured, _worker.IsRunning, _scheduler.IsPaused, _worker.LastReport, _worker.OtherMachines);
            }
        }
    }

    /// <summary>Every conflict waiting for the user; none while sync is off or once the repository changed (the coordinator forgets them with the state). Safe from any thread.</summary>
    public IReadOnlyList<SyncConflict> PendingConflicts() => IsConfigured ? _coordinator.PendingConflicts() : [];

    /// <summary>Starts the worker and, when a repository is set and automatic sync is on, the start-up run. Called once, from <c>SyncModule.Start</c>.</summary>
    public void Start()
    {
        if (_worker.Start())
        {
            _scheduler.Request(SyncTrigger.Start);
        }
    }

    /// <summary>Sync now: runs whatever the automatic state; with no repository set it does nothing.</summary>
    public void SyncNow()
    {
        if (IsConfigured)
        {
            _worker.Request(SyncTrigger.Manual);
        }
    }

    /// <summary>The answer to the join question: a run with it, which lifts the pause unless the repo asks again.</summary>
    public void Join(SyncJoin join) => _worker.Request(SyncTrigger.Join, join);

    /// <summary>Applies each resolution on the worker, in order, then runs so the result is published.</summary>
    public void Resolve(IReadOnlyList<SyncResolution> resolutions)
    {
        ArgumentNullException.ThrowIfNull(resolutions);
        if (resolutions.Count > 0)
        {
            _worker.Request(SyncTrigger.Resolve, resolutions: resolutions);
        }
    }

    public void Dispose()
    {
        lock (_worker.Gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _scheduler.StopLocked();
            _worker.StopLocked();
        }

        _storeThread.Stop();
        _scheduler.Detach();
        _worker.Join();
    }
}
