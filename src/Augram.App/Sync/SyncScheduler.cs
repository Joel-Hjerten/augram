using Augram.App.Hosting;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// When sync runs (F8 sync, "when"): listens to the stores and the settings, owns the change and poll timers and the
/// pause, and asks <see cref="SyncWorker.Request"/> for a run. <see cref="SyncTrigger.LocalChange"/>
/// <see cref="ChangeDelay"/> after the last gesture or mapping change the sync itself did not cause (told apart through
/// <see cref="SyncStoreThread.IsApplying"/>) or a new machine name; <see cref="SyncTrigger.Poll"/>
/// <see cref="PollInterval"/> after every run; <see cref="SyncTrigger.AutoSyncOn"/>; and
/// <see cref="SyncTrigger.RepositoryChanged"/> for a new URL, which lifts the pause. Automatic triggers
/// (<see cref="SyncTriggerExtensions.IsAutomatic"/>) are dropped while no repository is set,
/// <see cref="SyncSettings.AutoSync"/> is off, or the last run answered <see cref="SyncStatus.NeedsJoinChoice"/> or
/// <see cref="SyncStatus.NeedsUpdate"/> (<see cref="IsPaused"/>). Its state is under the worker's
/// <see cref="SyncWorker.Gate"/>, so the admission check and the queueing are one step, and the worker's run start
/// (timers cancelled) and run end (pause set, poll scheduled) happen under the same lock as the job's hand-over. The
/// store handlers run on the stores' (UI) thread; the timers' callbacks on pool threads.
/// </summary>
internal sealed class SyncScheduler
{
    public static readonly TimeSpan ChangeDelay = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly SyncWorker _worker;
    private readonly SettingsStore _settings;
    private readonly GestureLibrary _gestures;
    private readonly MappingStore _mapping;
    private readonly SyncStoreThread _storeThread;
    private readonly Func<TimeSpan, Action, IDisposable> _schedule;
    private SyncSettings _seen;

    // Guarded by the worker's Gate.
    private bool _stopped;
    private bool _paused;
    private IDisposable? _changeTimer;
    private IDisposable? _pollTimer;

    /// <remarks><c>schedule</c> runs the action once after the delay on any thread, and disposing its result cancels it; null is a <see cref="OneShotTimer"/> without a marshal, tests pass a manual one.</remarks>
    public SyncScheduler(
        SyncWorker worker,
        SettingsStore settings,
        GestureLibrary gestures,
        MappingStore mapping,
        SyncStoreThread storeThread,
        Func<TimeSpan, Action, IDisposable>? schedule = null)
    {
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _storeThread = storeThread ?? throw new ArgumentNullException(nameof(storeThread));
        _schedule = schedule ?? new OneShotTimer().Schedule;
        _seen = settings.Current.Sync;
        _worker.RunTaking += OnRunTaking;
        _worker.RunEnded += OnRunEnded;
        _settings.Changed += OnSettingsChanged;
        _gestures.Changed += OnStoreChanged;
        _mapping.Changed += OnStoreChanged;
    }

    /// <summary>
    /// True after a run answered <see cref="SyncStatus.NeedsJoinChoice"/> (until the user chooses) or <see cref="SyncStatus.NeedsUpdate"/>
    /// (until a run answers otherwise), or until the URL changes: automatic runs wait.
    /// </summary>
    public bool IsPaused
    {
        get
        {
            lock (_worker.Gate)
            {
                return _paused;
            }
        }
    }

    /// <summary>Asks the worker for a run; an automatic trigger only while automatic runs are allowed. Any thread.</summary>
    public void Request(SyncTrigger trigger)
    {
        lock (_worker.Gate)
        {
            if (_stopped || (trigger.IsAutomatic() && !AutomaticAllowedLocked()))
            {
                return;
            }

            _worker.Request(trigger);
        }
    }

    /// <summary>Under the worker's <see cref="SyncWorker.Gate"/>: cancels both timers and schedules nothing more.</summary>
    public void StopLocked()
    {
        _stopped = true;
        Cancel(ref _changeTimer);
        Cancel(ref _pollTimer);
    }

    /// <summary>Stops listening to the settings and the stores (after <see cref="StopLocked"/>).</summary>
    public void Detach()
    {
        _settings.Changed -= OnSettingsChanged;
        _gestures.Changed -= OnStoreChanged;
        _mapping.Changed -= OnStoreChanged;
    }

    private bool AutomaticAllowedLocked() => !_paused && _settings.Current.Sync is { IsOn: true, AutoSync: true };

    // Worker, under Gate: this run includes every change made so far; a change during it schedules a new delay.
    private void OnRunTaking()
    {
        Cancel(ref _changeTimer);
        Cancel(ref _pollTimer);
    }

    // Worker, under Gate.
    private void OnRunEnded(SyncReport report)
    {
        _paused = report.Status is SyncStatus.NeedsJoinChoice or SyncStatus.NeedsUpdate;
        if (_stopped)
        {
            return;
        }

        Cancel(ref _pollTimer);
        if (AutomaticAllowedLocked())
        {
            _pollTimer = _schedule(PollInterval, () => Request(SyncTrigger.Poll));
        }
    }

    private void ScheduleChange()
    {
        lock (_worker.Gate)
        {
            Cancel(ref _changeTimer);
            if (!_stopped && AutomaticAllowedLocked())
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
            lock (_worker.Gate)
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
                lock (_worker.Gate)
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

    private static void Cancel(ref IDisposable? timer)
    {
        timer?.Dispose();
        timer = null;
    }
}
