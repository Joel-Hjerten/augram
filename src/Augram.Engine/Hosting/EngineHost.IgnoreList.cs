using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.Engine.Hosting;

/// <summary>
/// The ignore list's face on the host (F5; README "Ignore list"): the <see cref="IgnoreListWatch"/> it creates with a
/// mapping keeps the hook's answer current; these members say what that answer is and let the App wake it after an edit.
/// </summary>
public sealed partial class EngineHost
{
    /// <summary>
    /// A "disable while focused" app gained or lost focus, or was renamed while it has it: the app, or null when Augram
    /// resumed. Raised on the ignore-list watch's thread (and on the stopping thread at <see cref="Stop"/>); the App marshals.
    /// </summary>
    public event EventHandler<IgnoredApp?>? PauseChanged;

    /// <summary>The focused "disable while focused" app Augram is paused for, or null: every press passes through meanwhile, as when disabled.</summary>
    public IgnoredApp? PausedBy => _ignoreWatch?.PausedBy;

    /// <summary>The ignored app under the pointer as of the watch's last pass, or null: the next stroke-button press passes through over it.</summary>
    public IgnoredApp? IgnoredUnderPointer => _ignoreWatch?.Over;

    /// <summary>For tests: the anchor plan the hook reads for the window under the pointer, as the watch last published it.</summary>
    internal AnchorPlan AnchorPlanUnderPointer => _gate.Plan;

    /// <summary>For tests: the hold remaps the hook reads at a hold key's press, the app in front's as the watch last published them (F9).</summary>
    internal HoldRemapPlan ForegroundHoldPlan => _gate.ForegroundPlan;

    /// <summary>For tests: messages whose replays the worker has not made yet (the hook keeps keys in order behind them).</summary>
    internal int PendingHoldReplays => _gate.Hold.PendingReplays;

    /// <summary>The mapping changed (an edit, undo, a sync): the ignore list's answer is worked out again without waiting for the pointer. Any thread; never blocks.</summary>
    public void MappingChanged()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            _ignoreWatch?.Wake();
        }
    }

    private void OnPauseChanged(IgnoredApp? app)
    {
        try
        {
            PauseChanged?.Invoke(this, app);
        }
        catch (Exception exception)
        {
            _log.Error(LogSources.Engine, "Pause handler threw", exception, ("app", app?.Name));
        }
    }
}
