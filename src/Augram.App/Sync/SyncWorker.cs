using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// The one background thread ("Augram sync") that runs sync jobs (F8 sync), so there is never more than one run at a
/// time: <see cref="Request"/> queues a job and requests made while a run is going (or queued) coalesce into one more
/// run (the user's reason wins over an automatic one; join answers and resolutions accumulate). Each run is one
/// <see cref="SyncCoordinator.Run"/> with the job's join answer and resolutions; an exception inside it is logged and
/// reported as <see cref="SyncStatus.Failed"/>, and the worker keeps serving. It decides nothing about when to run:
/// that is <see cref="SyncScheduler"/>, which shares <see cref="Gate"/> and hears <see cref="RunTaking"/> and
/// <see cref="RunEnded"/> while the worker holds it. <see cref="Changed"/> is raised on the worker when a run starts
/// and when it ends; consumers marshal. <see cref="SyncService"/> owns both and the stop order.
/// </summary>
internal sealed class SyncWorker
{
    public static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly SyncCoordinator _coordinator;
    private readonly SyncBaseStore _bases;
    private readonly IClock _clock;
    private readonly IEventLog _log;
    private readonly AutoResetEvent _wake = new(false);
    private readonly List<SyncResolution> _resolutions = [];
    private Thread? _thread;

    // Guarded by Gate.
    private bool _started;
    private bool _stopped;
    private bool _requested;
    private SyncTrigger _trigger;
    private SyncJoin? _join;
    private bool _running;
    private SyncReport? _last;
    private SyncTrigger? _lastTrigger;
    private IReadOnlyList<string> _otherMachines;
    private int _runs;

    public SyncWorker(SyncCoordinator coordinator, SyncBaseStore bases, IClock clock, IEventLog log)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _bases = bases ?? throw new ArgumentNullException(nameof(bases));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _otherMachines = MachineNames();
    }

    /// <summary>Raised on the worker when a run starts and when it ends; handlers marshal to the UI thread themselves and return at once.</summary>
    public event EventHandler? Changed;

    /// <summary>On the worker, under <see cref="Gate"/>, as a queued job is taken, before it runs: the scheduler cancels its timers (the run includes every change made so far).</summary>
    public event Action? RunTaking;

    /// <summary>On the worker, under <see cref="Gate"/>, once a run's report is stored and before the stop check: the scheduler sets the pause and schedules the poll.</summary>
    public event Action<SyncReport>? RunEnded;

    /// <summary>The one lock for everything the worker and the scheduler share; <see cref="SyncService"/> reads the status under it too.</summary>
    public object Gate { get; } = new();

    public bool IsRunning => Locked(() => _running);

    /// <summary>The last run's report; null before the first run.</summary>
    public SyncReport? LastReport => Locked(() => _last);

    /// <summary>What started the last run; null before the first run.</summary>
    public SyncTrigger? LastTrigger => Locked(() => _lastTrigger);

    /// <summary>The names of the machines this one has synced with, as of the last run ("from Mac").</summary>
    public IReadOnlyList<string> OtherMachines => Locked(() => _otherMachines);

    /// <summary>How many runs have finished (tests and diagnostics).</summary>
    public int RunCount => Locked(() => _runs);

    /// <summary>Starts the thread; false when it was already started or the worker is stopped.</summary>
    public bool Start()
    {
        lock (Gate)
        {
            if (_started || _stopped)
            {
                return false;
            }

            _started = true;
            _thread = new Thread(Work) { IsBackground = true, Name = "Augram sync" };
            _thread.Start();
            return true;
        }
    }

    /// <summary>Queues a run (or joins the queued one); nothing once stopped. Any thread.</summary>
    public void Request(SyncTrigger trigger, SyncJoin? join = null, IReadOnlyList<SyncResolution>? resolutions = null)
    {
        lock (Gate)
        {
            if (_stopped)
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

    /// <summary>Under <see cref="Gate"/>: no request is taken from now on and the thread is woken to end.</summary>
    public void StopLocked()
    {
        _stopped = true;
        _wake.Set();
    }

    /// <summary>Waits up to <see cref="StopTimeout"/> for the thread after <see cref="StopLocked"/>; a run inside a git call is abandoned on its background thread.</summary>
    public void Join()
    {
        var thread = _thread;
        if (thread is null || thread == Thread.CurrentThread || thread.Join(StopTimeout))
        {
            // Under the lock: a timer callback already past its own check may still be inside Request.
            lock (Gate)
            {
                _wake.Dispose();
            }
        }
    }

    private void Work()
    {
        while (true)
        {
            _wake.WaitOne();
            Job job;
            lock (Gate)
            {
                if (_stopped)
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
                RunTaking?.Invoke();
            }

            RaiseChanged();
            var report = Execute(job);
            var others = MachineNames();
            lock (Gate)
            {
                _running = false;
                _last = report;
                _lastTrigger = job.Trigger;
                _otherMachines = others;
                _runs++;
                RunEnded?.Invoke(report);
                if (_stopped)
                {
                    return;
                }
            }

            RaiseChanged();
        }
    }

    private SyncReport Execute(Job job)
    {
        try
        {
            return _coordinator.Run(job.Join, job.Resolutions);
        }
        catch (Exception exception)
        {
            // A bug must not end the worker: report it and keep serving requests.
            _log.Error(LogSources.Sync, "Sync failed unexpectedly", exception);
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

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            // A handler's bug must not end the worker thread (and with it the process).
            _log.Error(LogSources.Sync, "A sync status handler failed", exception);
        }
    }

    private T Locked<T>(Func<T> read)
    {
        lock (Gate)
        {
            return read();
        }
    }

    private sealed record Job(SyncTrigger Trigger, SyncJoin? Join, IReadOnlyList<SyncResolution> Resolutions);
}
