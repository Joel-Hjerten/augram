using System.Diagnostics;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.App.Hosting;

/// <summary>
/// A launch's side of one Augram at a time (A10), across the installed and the development builds (Joel, 2026-10-08).
/// <see cref="Program"/> calls <see cref="Begin"/> before anything else exists. It takes the single-instance name when it is
/// free (<see cref="InstanceStartupStep.Run"/>). Otherwise it says hello to the running Augram: the same install (same
/// channel and executable) is asked to show its window and this launch exits, as a second launch always did; an Augram from
/// before identities has already shown itself on the connection; one that does not answer gets a few seconds to let go of
/// the name. A different build means <see cref="InstanceStartupStep.Choose"/>: once Avalonia runs, but before the engine,
/// hook, overlay or sync start, <see cref="ChooseAsync"/> asks "Quit it and start this one?". On yes it asks the running one to
/// quit over the pipe and waits up to <see cref="QuitTimeout"/> for the name (the running one releases it last, after its
/// engine stopped and its config was flushed); on Cancel the running one is shown. What happened is kept in <see cref="LogNotes"/>
/// for the log, which does not exist yet while this runs; a launch that exits leaves its side in the running one's log
/// (every request carries this launch's identity and reason). Disposing releases the name.
/// </summary>
public sealed class InstanceStartup : IDisposable
{
    public const string LogSource = "app";
    public const string SameInstallReason = "same install";
    public const string CancelledReason = "take-over cancelled";
    public const string ConfirmLabel = "Quit it and start this one";

    /// <summary>How long a request to the running Augram may take to connect and be answered.</summary>
    public static readonly TimeSpan ContactTimeout = TimeSpan.FromSeconds(2);

    public static readonly TimeSpan DefaultQuitTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan DefaultSilentWait = TimeSpan.FromSeconds(3);

    private readonly string _name;
    private readonly TimeSpan _silentWait;
    private readonly List<(string Message, LogProperty[] Properties)> _notes = [];

    /// <param name="name">The single-instance name; tests use a unique one.</param>
    /// <param name="self">This launch.</param>
    /// <param name="quitTimeout">How long a confirmed take-over waits for the running one to quit; <see cref="DefaultQuitTimeout"/> when null.</param>
    /// <param name="silentWait">How long to wait for a running one that holds the name but does not answer; <see cref="DefaultSilentWait"/> when null.</param>
    public InstanceStartup(string name, InstanceIdentity self, TimeSpan? quitTimeout = null, TimeSpan? silentWait = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(self);
        _name = name;
        Self = self;
        QuitTimeout = quitTimeout ?? DefaultQuitTimeout;
        _silentWait = silentWait ?? DefaultSilentWait;
    }

    public InstanceIdentity Self { get; }

    public TimeSpan QuitTimeout { get; }

    /// <summary>This launch's guard once it holds the name; null before, and for a launch that exits.</summary>
    public SingleInstanceGuard? Guard { get; private set; }

    /// <summary>The running Augram of a different build that <see cref="Begin"/> found; null otherwise.</summary>
    public InstanceIdentity? Running { get; private set; }

    /// <summary>True between a <see cref="InstanceStartupStep.Choose"/> from <see cref="Begin"/> and the end of <see cref="ChooseAsync"/>.</summary>
    public bool NeedsChoice => Guard is null && Running is not null;

    /// <summary>The take-over question: "Augram 0.2.0 (installed) is already running. Quit it and start Augram (Dev) instead?"</summary>
    public static string Question(InstanceIdentity running, InstanceIdentity self)
    {
        ArgumentNullException.ThrowIfNull(running);
        ArgumentNullException.ThrowIfNull(self);
        if (running.App.Channel != self.App.Channel)
        {
            return $"{running.App.Describe()} is already running. Quit it and start {StartName(self)} instead?";
        }

        // Same channel, another executable: two development copies (another folder, Debug and Release). Only the paths tell them apart.
        return $"{running.App.Describe()} is already running from another folder. Quit it and start this one instead?"
            + $"\n\nRunning: {running.ExecutablePath}\nThis one: {self.ExecutablePath}";
    }

    /// <summary>What the user is told when the running one did not quit in time; this launch then exits.</summary>
    public static string TimeoutMessage(InstanceIdentity running, InstanceIdentity self, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(running);
        ArgumentNullException.ThrowIfNull(self);
        return $"{running.App.Describe()} did not quit within {Math.Round(timeout.TotalSeconds)} seconds. Quit it from its tray icon, then start {StartName(self)} again.";
    }

    /// <summary>Before Avalonia and before any service: take the name, or settle with the running Augram, or find that a choice is needed.</summary>
    public InstanceStartupStep Begin()
    {
        Guard = SingleInstanceGuard.TryAcquire(_name, Self);
        if (Guard is not null)
        {
            return InstanceStartupStep.Run;
        }

        var answer = SingleInstanceGuard.Send(_name, new InstanceRequest(InstanceRequestKind.Hello, Self), ContactTimeout);
        switch (answer.Kind)
        {
            case PeerAnswerKind.Answered when answer.Running!.IsSameInstallAs(Self):
                SingleInstanceGuard.Send(_name, new InstanceRequest(InstanceRequestKind.Show, Self, SameInstallReason), ContactTimeout);
                return InstanceStartupStep.Exit;
            case PeerAnswerKind.Answered:
                Running = answer.Running;
                return InstanceStartupStep.Choose;
            case PeerAnswerKind.Older:
                // An Augram from before identities showed its window on the connection; it cannot be asked to quit.
                return InstanceStartupStep.Exit;
            default:
                // It holds the name but does not answer: it is starting or shutting down. Give it a moment to let go.
                Guard = SingleInstanceGuard.AcquireAsync(_name, Self, _silentWait).GetAwaiter().GetResult();
                if (Guard is null)
                {
                    return InstanceStartupStep.Exit;
                }

                Note("Started after a silent Augram let go of the instance", ("waitedUpToMs", (int)_silentWait.TotalMilliseconds));
                return InstanceStartupStep.Run;
        }
    }

    /// <summary>
    /// On the UI thread, after <see cref="Begin"/> answered <see cref="InstanceStartupStep.Choose"/>: asks the user, and on yes
    /// takes over. True when this launch now holds the name (<see cref="Guard"/>) and should start; false when it should exit.
    /// </summary>
    public async Task<bool> ChooseAsync(ITakeOverPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        if (Guard is not null)
        {
            return true;
        }

        var running = Running ?? throw new InvalidOperationException("Begin found no other Augram to choose about.");
        var title = Self.App.DisplayName;
        if (!await presenter.ConfirmAsync(title, Question(running, Self), ConfirmLabel).ConfigureAwait(true))
        {
            await Task.Run(() => SingleInstanceGuard.Send(_name, new InstanceRequest(InstanceRequestKind.Show, Self, CancelledReason), ContactTimeout)).ConfigureAwait(true);
            return false;
        }

        var started = Stopwatch.GetTimestamp();
        var answer = await Task.Run(() => SingleInstanceGuard.Send(_name, new InstanceRequest(InstanceRequestKind.Quit, Self), ContactTimeout)).ConfigureAwait(true);
        Guard = await SingleInstanceGuard.AcquireAsync(_name, Self, QuitTimeout).ConfigureAwait(true);
        if (Guard is null)
        {
            await presenter.InformAsync(title, TimeoutMessage(running, Self, QuitTimeout)).ConfigureAwait(true);
            return false;
        }

        Note("Took over from another Augram",
            ("running", running.App.Describe()),
            ("runningChannel", running.App.Channel),
            ("runningVersion", running.App.InformationalVersion),
            ("runningPath", running.ExecutablePath),
            ("quitAnswered", answer.Kind == PeerAnswerKind.Answered),
            ("waitedMs", (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds));
        return true;
    }

    /// <summary>Writes what happened before the log existed (a take-over, a wait) to <paramref name="log"/>, once.</summary>
    public void LogNotes(IEventLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        foreach (var (message, properties) in _notes)
        {
            log.Info(LogSource, message, properties);
        }

        _notes.Clear();
    }

    public void Dispose() => Guard?.Dispose();

    private static string StartName(InstanceIdentity self) => self.App.IsDev ? AppInfo.DevDisplayName : "the installed Augram";

    private void Note(string message, params LogProperty[] properties) => _notes.Add((message, properties));
}
