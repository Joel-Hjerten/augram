using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Sync;

/// <summary>
/// Runs one sync (F8 sync; README: the run) and applies conflict resolutions. Threading contract: <see cref="Run"/>
/// and <see cref="Resolve"/> are called from the sync worker only, never from the store (UI) thread; the git work,
/// the merge and the state files happen there, and every store mutation goes through <c>onStoreThread</c>, which the
/// host runs on the thread that owns the stores (tests pass <c>f =&gt; f()</c>), against a snapshot it re-checks
/// (<see cref="SyncStoreWriter"/>). Applying is one <c>ReplaceAll</c> per store that changed, so one undo step per
/// store, and the config file's backup-before-write keeps the previous file. Logs one line per run under
/// <see cref="LogSource"/> with counts only: never content, never more of the URL than its host.
/// </summary>
public sealed class SyncCoordinator
{
    public const string LogSource = "sync";
    public const int MaxAttempts = 3;

    private readonly object _gate = new();
    private readonly ISyncRepository _repository;
    private readonly SettingsStore _settings;
    private readonly SyncBaseStore _bases;
    private readonly IClock _clock;
    private readonly IEventLog _log;
    private readonly Func<Func<SyncApplied>, SyncApplied> _onStoreThread;
    private readonly StepRegistry _steps;
    private readonly SyncStoreWriter _writer;
    private readonly SyncPublisher _publisher;
    private IReadOnlyList<SyncConflict> _pending;

    /// <param name="repository">The repo adapter (<c>Augram.Sync.Git</c>).</param>
    /// <param name="settings">Read for the sync section; written once, through <paramref name="onStoreThread"/>, to generate the machine id.</param>
    /// <param name="gestures">Merged into, through <paramref name="onStoreThread"/>.</param>
    /// <param name="mapping">Merged into, through <paramref name="onStoreThread"/>.</param>
    /// <param name="bases">The local sync state.</param>
    /// <param name="clock">Stamps files, reports and conflicts.</param>
    /// <param name="log">One line per run, source <see cref="LogSource"/>.</param>
    /// <param name="onStoreThread">Runs the function on the stores' thread and returns its result.</param>
    /// <param name="steps">The step types other machines' files may use; <see cref="StepRegistry.BuiltIn"/> by default.</param>
    public SyncCoordinator(
        ISyncRepository repository,
        SettingsStore settings,
        GestureLibrary gestures,
        MappingStore mapping,
        SyncBaseStore bases,
        IClock clock,
        IEventLog log,
        Func<Func<SyncApplied>, SyncApplied> onStoreThread,
        StepRegistry? steps = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _bases = bases ?? throw new ArgumentNullException(nameof(bases));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _onStoreThread = onStoreThread ?? throw new ArgumentNullException(nameof(onStoreThread));
        _steps = steps ?? StepRegistry.BuiltIn;
        _writer = new SyncStoreWriter(
            gestures ?? throw new ArgumentNullException(nameof(gestures)),
            mapping ?? throw new ArgumentNullException(nameof(mapping)),
            onStoreThread);
        _publisher = new SyncPublisher(repository, bases, clock);
        _pending = bases.PendingConflicts();
    }

    /// <summary>
    /// Every conflict waiting for the user, across machines (Options › Sync), as of construction or the last
    /// <see cref="Run"/> or <see cref="Resolve"/>. Safe from any thread, the UI thread included: it never waits for a run.
    /// </summary>
    public IReadOnlyList<SyncConflict> PendingConflicts() => Volatile.Read(ref _pending);

    /// <summary>Pulls, merges every other machine's file, applies the result and publishes this machine's file.</summary>
    /// <param name="join">The answer to an earlier <see cref="SyncStatus.NeedsJoinChoice"/>; ignored once this machine has synced.</param>
    public SyncReport Run(SyncJoin? join = null)
    {
        lock (_gate)
        {
            if (_settings.Current.Sync.RepositoryUrl is not { } url)
            {
                return new SyncReport(SyncStatus.Off, _clock.UtcNow);
            }

            SyncReport report;
            try
            {
                report = RunLocked(url, join);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                report = Failed($"The sync state could not be written: {ex.Message}");
            }

            Log(report, SyncSettingsRules.Host(url), "Sync");
            Volatile.Write(ref _pending, _bases.PendingConflicts());
            return report;
        }
    }

    /// <summary>Applies the user's choice for one pending conflict (on the store thread) and settles its base; the next <see cref="Run"/> publishes it.</summary>
    public SyncReport Resolve(SyncConflict conflict, SyncChoice choice)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        lock (_gate)
        {
            SyncReport report;
            try
            {
                report = ResolveLocked(conflict, choice);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ConfigFormatException)
            {
                report = Failed($"The conflict could not be resolved: {ex.Message}") with { Conflicts = _bases.PendingConflicts() };
            }

            Log(report, SyncSettingsRules.Host(_settings.Current.Sync.RepositoryUrl), $"Sync conflict resolved ({conflict.Kind}, {choice})");
            Volatile.Write(ref _pending, _bases.PendingConflicts());
            return report;
        }
    }

    private SyncReport RunLocked(string url, SyncJoin? join)
    {
        var self = EnsureMachineId();
        foreach (var operation in new Func<SyncOperationResult>[] { () => _repository.Prepare(url), _repository.Pull })
        {
            var result = operation();
            if (!result.Succeeded)
            {
                return Failed(result.Error ?? "The repository operation failed.");
            }
        }

        var files = SyncRepositoryFiles.Read(_repository.ReadMachineFiles(), self, _steps);
        if (NewerThanThisBuild(self, files) is { Count: > 0 } newer)
        {
            // Merging would misread what this build cannot see, and publishing would overwrite it: stop before either.
            return new SyncReport(SyncStatus.NeedsUpdate, _clock.UtcNow)
            {
                NewerMachines = newer,
                Notes = [.. newer.Select(machine => machine.Description), .. files.Notes],
            };
        }

        bool joining = _bases.IsEmpty && files.Others.Count > 0;
        if (joining && join is null)
        {
            return new SyncReport(SyncStatus.NeedsJoinChoice, _clock.UtcNow) { OtherMachines = files.Summaries(), Notes = files.Notes };
        }

        var states = _bases.Machines().ToDictionary(state => state.MachineId);
        SyncPlanner.Plan? plan = null;
        var error = _writer.ComputeAndApply((gestures, mapping) =>
        {
            plan = SyncPlanner.Run(self, gestures, mapping, files.Others, states, _bases.Published, joining ? join : null, _clock.UtcNow);
            return (plan.Gestures, plan.Mapping, plan.Counts);
        });
        if (error is not null || plan is null)
        {
            return Failed(error ?? "Nothing was merged.") with { Notes = files.Notes };
        }

        foreach (var state in plan.States)
        {
            _bases.Save(state);
            states[state.MachineId] = state;
        }

        var publishError = _publisher.PublishIfNeeded(self, _settings.Current.Sync.MachineName, files.Own, plan, states.Values);
        return new SyncReport(publishError is not null ? SyncStatus.Failed : plan.Counts.IsEmpty ? SyncStatus.UpToDate : SyncStatus.Applied, _clock.UtcNow)
        {
            Counts = plan.Counts,
            Conflicts = states.Values.SelectMany(state => state.Conflicts).ToArray(),
            Repairs = plan.Repairs,
            Notes = files.Notes,
            Error = publishError,
        };
    }

    private SyncReport ResolveLocked(SyncConflict conflict, SyncChoice choice)
    {
        var state = _bases.Machine(conflict.MachineId);
        var pending = state?.Conflicts.FirstOrDefault(candidate => candidate.Key == conflict.Key);
        if (state is null || pending is null)
        {
            return new SyncReport(SyncStatus.UpToDate, _clock.UtcNow) { Conflicts = _bases.PendingConflicts(), Notes = ["That conflict is no longer pending."] };
        }

        var (effective, note) = ConflictResolution.Effective(pending, choice);
        SyncMergeResult? result = null;
        if (effective != SyncChoice.KeepMine)
        {
            var error = _writer.ComputeAndApply((gestures, mapping) =>
            {
                result = ConflictResolution.Apply(SyncItemSet.From(gestures, mapping), pending, effective, _steps);
                return (result.Gestures, result.Mapping, result.Counts);
            });
            if (error is not null)
            {
                return Failed(error) with { Conflicts = _bases.PendingConflicts() };
            }
        }

        // Theirs has now been seen and answered: it is the base, so the next merge reads ours as the newer change.
        var bases = new Dictionary<SyncItemKey, string>(state.Base);
        if (pending.RemoteContent is { } remote)
        {
            bases[pending.Key] = remote;
        }
        else
        {
            bases.Remove(pending.Key);
        }

        _bases.Save(state with { Base = bases, Conflicts = state.Conflicts.Where(candidate => candidate.Key != pending.Key).ToArray() });
        var counts = result?.Counts ?? SyncCounts.None;
        return new SyncReport(counts.IsEmpty ? SyncStatus.UpToDate : SyncStatus.Applied, _clock.UtcNow)
        {
            Counts = counts,
            Repairs = result?.Repairs ?? [],
            Conflicts = _bases.PendingConflicts(),
            Notes = note is null ? [] : [note],
        };
    }

    /// <summary>The files a newer Augram wrote, and this machine when it has published in a newer format than this build writes.</summary>
    private List<SyncNewerMachine> NewerThanThisBuild(Guid self, SyncRepositoryFiles files)
    {
        var newer = files.Newer.ToList();
        int published = _bases.PublishedFormatVersion;
        if (published > SyncFile.CurrentFormatVersion && !newer.Any(machine => machine.IsThisMachine))
        {
            newer.Add(new SyncNewerMachine(self, _settings.Current.Sync.MachineName, IsThisMachine: true, published, ConfigDocument.CurrentSchemaVersion));
        }

        return newer;
    }

    private Guid EnsureMachineId()
    {
        if (_settings.Current.Sync.MachineId == Guid.Empty)
        {
            _onStoreThread(() =>
            {
                _settings.EnsureMachineId();
                return SyncApplied.Done;
            });
        }

        return _settings.Current.Sync.MachineId;
    }

    private SyncReport Failed(string error) => new(SyncStatus.Failed, _clock.UtcNow) { Error = error };

    private void Log(SyncReport report, string host, string what)
    {
        if (report.Status == SyncStatus.NeedsUpdate)
        {
            _log.Warning(
                LogSource,
                $"{what} paused: a newer Augram wrote to the repository",
                ("host", host),
                ("newest format", report.NewerMachines.Max(machine => machine.FormatVersion)),
                ("this build", SyncFile.CurrentFormatVersion));
            return;
        }

        if (report.Status == SyncStatus.Failed)
        {
            _log.Warning(LogSource, $"{what} failed", ("host", host), ("error", report.Error));
            return;
        }

        _log.Info(
            LogSource,
            $"{what}: {report.Status}",
            ("host", host),
            ("changes", report.Counts.ToString()),
            ("conflicts", report.Conflicts.Count),
            ("repairs", report.Repairs.Count),
            ("notes", report.Notes.Count));
    }
}
