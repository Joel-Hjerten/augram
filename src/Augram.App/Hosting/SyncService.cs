using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;

namespace Augram.App.Hosting;

/// <summary>
/// Runs machine-to-machine sync (F8 sync) on one dedicated background worker, so there is never more than one run
/// at a time and requests made while a run is going (or queued) coalesce into one more run. Triggers
/// (<see cref="SyncTrigger"/>): once at <see cref="Start"/>, <see cref="SyncNow"/>, <see cref="ChangeDelay"/> after
/// the last gesture or mapping change the sync itself did not cause (it tells them apart through
/// <see cref="SyncStoreThread.IsApplying"/>), every <see cref="PollInterval"/> while automatic sync is on, a new
/// repository URL, the join answer (<see cref="Join"/>) and conflict resolutions (<see cref="Resolve"/>). With no
/// repository set nothing runs, and automatic triggers wait while <see cref="SyncSettings.AutoSync"/> is off or a
/// <see cref="SyncStatus.NeedsJoinChoice"/> report is waiting for the user (<see cref="IsPaused"/>). Before every run
/// the worker lets <see cref="SyncFolders"/> reset the state and move the clone aside when the URL changed.
/// <see cref="Changed"/> is raised on the worker when a run starts and when it ends; consumers marshal. The store
/// handlers run on the stores' (UI) thread; the timers' callbacks on pool threads; everything shared is under one
/// lock. <see cref="Dispose"/> stops the store thread first (nothing is applied after it), wakes the worker and waits
/// briefly: a run inside a git call is abandoned on its background thread, which applies and saves nothing more.
/// Under <c>--no-engine</c> sync works the same: it never touches input.
/// </summary>
public sealed class SyncService : IDisposable
{
    public const string LogSource = SyncCoordinator.LogSource;

    public static readonly TimeSpan ChangeDelay = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly SyncCoordinator _coordinator;
    private readonly SettingsStore _settings;
    private readonly GestureLibrary _gestures;
    private readonly MappingStore _mapping;
    private readonly SyncBaseStore _bases;
    private readonly SyncFolders _folders;
    private readonly SyncStoreThread _storeThread;
    private readonly IClock _clock;
    private readonly IEventLog _log;
    private readonly Func<TimeSpan, Action, IDisposable> _schedule;
    private readonly AutoResetEvent _wake = new(false);
    private readonly List<SyncResolution> _resolutions = [];
    private Thread? _worker;
    private SyncSettings _seen;

    // Guarded by _gate.
    private bool _started;
    private bool _disposed;
    private bool _requested;
    private SyncTrigger _trigger;
    private SyncJoin? _join;
    private bool _running;
    private bool _paused;
    private bool _stateCleared;
    private IDisposable? _changeTimer;
    private IDisposable? _pollTimer;
    private SyncReport? _last;
    private SyncTrigger? _lastTrigger;
    private IReadOnlyList<string> _otherMachines;
    private int _runs;

    /// <remarks><c>schedule</c> runs the action once after the delay on any thread, and disposing its result cancels it; null is a <see cref="TimerSaveScheduler"/> per call, tests pass a manual one.</remarks>
    public SyncService(
        SyncCoordinator coordinator,
        SettingsStore settings,
        GestureLibrary gestures,
        MappingStore mapping,
        SyncBaseStore bases,
        SyncFolders folders,
        SyncStoreThread storeThread,
        IClock clock,
        IEventLog log,
        Func<TimeSpan, Action, IDisposable>? schedule = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _bases = bases ?? throw new ArgumentNullException(nameof(bases));
        _folders = folders ?? throw new ArgumentNullException(nameof(folders));
        _storeThread = storeThread ?? throw new ArgumentNullException(nameof(storeThread));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _schedule = schedule ?? ((delay, action) => new TimerSaveScheduler(delay).Schedule(action));
        _seen = settings.Current.Sync;
        _otherMachines = MachineNames();
        _settings.Changed += OnSettingsChanged;
        _gestures.Changed += OnStoreChanged;
        _mapping.Changed += OnStoreChanged;
    }

    /// <summary>Raised on the worker when a run starts and when it ends; handlers marshal to the UI thread themselves and return at once.</summary>
    public event EventHandler? Changed;

    /// <summary>True when a repository is set.</summary>
    public bool IsConfigured => _settings.Current.Sync.IsOn;

    public bool IsRunning => Locked(() => _running);

    /// <summary>True after a run answered <see cref="SyncStatus.NeedsJoinChoice"/> and until the user chooses (or the URL changes): automatic runs wait.</summary>
    public bool IsPaused => Locked(() => _paused);

    /// <summary>The last run's report; null before the first run.</summary>
    public SyncReport? LastReport => Locked(() => _last);

    /// <summary>What started the last run; null before the first run.</summary>
    public SyncTrigger? LastTrigger => Locked(() => _lastTrigger);

    /// <summary>The names of the machines this one has synced with, as of the last run ("from Mac").</summary>
    public IReadOnlyList<string> OtherMachines => Locked(() => _otherMachines);

    /// <summary>How many runs have finished (tests and diagnostics).</summary>
    public int RunCount => Locked(() => _runs);

    /// <summary>Every conflict waiting for the user; none while sync is off or right after the repository changed. Safe from any thread.</summary>
    public IReadOnlyList<SyncConflict> PendingConflicts()
    {
        lock (_gate)
        {
            if (_stateCleared || !IsConfigured)
            {
                return [];
            }
        }

        return _coordinator.PendingConflicts();
    }

    /// <summary>Starts the worker and, when a repository is set and automatic sync is on, the start-up run. Called once, from <c>SyncModule.Start</c>.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _worker = new Thread(Work) { IsBackground = true, Name = "Augram sync" };
            _worker.Start();
        }

        Request(SyncTrigger.Start);
    }

    /// <summary>Sync now: runs whatever the automatic state; with no repository set it does nothing.</summary>
    public void SyncNow()
    {
        if (IsConfigured)
        {
            Request(SyncTrigger.Manual);
        }
    }

    /// <summary>The answer to the join question: a run with it, which lifts the pause unless the repo asks again.</summary>
    public void Join(SyncJoin join) => Request(SyncTrigger.Join, join);

    /// <summary>Applies each resolution on the worker, in order, then runs so the result is published.</summary>
    public void Resolve(IReadOnlyList<SyncResolution> resolutions)
    {
        ArgumentNullException.ThrowIfNull(resolutions);
        if (resolutions.Count > 0)
        {
            Request(SyncTrigger.Resolve, resolutions: resolutions);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Cancel(ref _changeTimer);
            Cancel(ref _pollTimer);
            _wake.Set();
        }

        _storeThread.Stop();
        _settings.Changed -= OnSettingsChanged;
        _gestures.Changed -= OnStoreChanged;
        _mapping.Changed -= OnStoreChanged;
        var worker = _worker;
        if (worker is null || worker == Thread.CurrentThread || worker.Join(StopTimeout))
        {
            // Under the lock: a timer callback already past its own check may still be inside Request.
            lock (_gate)
            {
                _wake.Dispose();
            }
        }
    }

    private void Request(SyncTrigger trigger, SyncJoin? join = null, IReadOnlyList<SyncResolution>? resolutions = null)
    {
        lock (_gate)
        {
            if (_disposed || (trigger.IsAutomatic() && !AutomaticAllowedLocked()))
            {
                return;
            }

            // Coalesce: one run serves every request made before it starts; the user's reason wins over an automatic one.
            if (!_requested || !trigger.IsAutomatic())
            {
                _trigger = trigger;
            }

            _requested = true;
            _join = join ?? _join;
            if (resolutions is not null)
            {
                _resolutions.AddRange(resolutions);
            }

            _wake.Set();
        }
    }

    private bool AutomaticAllowedLocked() => !_paused && _settings.Current.Sync is { IsOn: true, AutoSync: true };

    private void Work()
    {
        while (true)
        {
            _wake.WaitOne();
            Job job;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (!_requested)
                {
                    continue;
                }

                job = new Job(_trigger, _join, [.. _resolutions]);
                _requested = false;
                _join = null;
                _resolutions.Clear();
                _running = true;

                // This run includes every change made so far; a change during it schedules a new delay.
                Cancel(ref _changeTimer);
                Cancel(ref _pollTimer);
            }

            RaiseChanged();
            var report = Execute(job);
            var others = MachineNames();
            lock (_gate)
            {
                _running = false;
                _last = report;
                _lastTrigger = job.Trigger;
                _otherMachines = others;
                _paused = report.Status == SyncStatus.NeedsJoinChoice;
                _stateCleared &= report.Status == SyncStatus.Off;
                _runs++;
                if (_disposed)
                {
                    return;
                }

                SchedulePollLocked();
            }

            RaiseChanged();
        }
    }

    private SyncReport Execute(Job job)
    {
        try
        {
            var url = _settings.Current.Sync.RepositoryUrl;
            var reset = _folders.Prepare(url, _bases);
            if (reset.Reset)
            {
                lock (_gate)
                {
                    _stateCleared = true;
                }

                _log.Info(LogSource, "Sync state cleared: the repository changed", ("host", SyncSettingsRules.Host(url)), ("old clone kept", reset.MovedTo is not null));
            }

            if (url is null)
            {
                return _coordinator.Run();
            }

            var notes = new List<string>();
            foreach (var resolution in job.Resolutions)
            {
                var resolved = _coordinator.Resolve(resolution.Conflict, resolution.Choice);
                notes.AddRange(resolved.Notes);
                if (resolved.Status == SyncStatus.Failed)
                {
                    return resolved with { Notes = notes };
                }
            }

            var report = _coordinator.Run(job.Join);
            return notes.Count == 0 ? report : report with { Notes = [.. notes, .. report.Notes] };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _log.Warning(LogSource, "Sync folder could not be prepared", ("error", exception.Message));
            return Failed($"The old sync folder could not be moved aside: {exception.Message}");
        }
        catch (Exception exception)
        {
            // A bug must not end the worker: report it and keep serving requests.
            _log.Error(LogSource, "Sync failed unexpectedly", exception);
            return Failed($"Sync failed unexpectedly: {exception.Message}");
        }
    }

    private SyncReport Failed(string error) => new(SyncStatus.Failed, _clock.UtcNow) { Error = error };

    private IReadOnlyList<string> MachineNames()
    {
        try
        {
            return [.. _bases.Machines().Select(state => state.MachineName).Order(StringComparer.CurrentCultureIgnoreCase)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void SchedulePollLocked()
    {
        Cancel(ref _pollTimer);
        if (!_disposed && AutomaticAllowedLocked())
        {
            _pollTimer = _schedule(PollInterval, () => Request(SyncTrigger.Poll));
        }
    }

    private void ScheduleChange()
    {
        lock (_gate)
        {
            Cancel(ref _changeTimer);
            if (!_disposed && AutomaticAllowedLocked())
            {
                _changeTimer = _schedule(ChangeDelay, () => Request(SyncTrigger.LocalChange));
            }
        }
    }

    // Store thread, inside the store's change: a change the sync applies is not a local change to publish.
    private void OnStoreChanged(object? sender, EventArgs e)
    {
        if (!_storeThread.IsApplying)
        {
            ScheduleChange();
        }
    }

    // Store thread (UI). Settings are not synced; only the sync section matters here.
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        var now = _settings.Current.Sync;
        var before = _seen;
        _seen = now;
        if (!string.Equals(now.RepositoryUrl, before.RepositoryUrl, StringComparison.Ordinal))
        {
            lock (_gate)
            {
                _paused = false;
                Cancel(ref _changeTimer);
                Cancel(ref _pollTimer);
            }

            // The worker resets the state for the new repository (or for none) before anything else.
            Request(SyncTrigger.RepositoryChanged);
            return;
        }

        if (now.AutoSync != before.AutoSync)
        {
            if (now.AutoSync)
            {
                Request(SyncTrigger.AutoSyncOn);
            }
            else
            {
                lock (_gate)
                {
                    Cancel(ref _changeTimer);
                    Cancel(ref _pollTimer);
                }
            }
        }

        if (!string.Equals(now.MachineName, before.MachineName, StringComparison.Ordinal))
        {
            ScheduleChange();
        }
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            // A handler's bug must not end the worker thread (and with it the process).
            _log.Error(LogSource, "A sync status handler failed", exception);
        }
    }

    private T Locked<T>(Func<T> read)
    {
        lock (_gate)
        {
            return read();
        }
    }

    private static void Cancel(ref IDisposable? timer)
    {
        timer?.Dispose();
        timer = null;
    }

    private sealed record Job(SyncTrigger Trigger, SyncJoin? Join, IReadOnlyList<SyncResolution> Resolutions);
}
