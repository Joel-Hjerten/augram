using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Remap;

namespace Augram.Engine.Hosting;

/// <summary>
/// The logging half of <see cref="EngineWorker"/>'s hold remaps (F9): the cross-check of the hook's decision against the
/// machine's, and the <c>hold</c> Debug lines per hold. Runs after the outcomes were acted on (acts first, logs second); split
/// from <c>EngineWorker.HoldRemaps.cs</c>, the same class.
/// </summary>
internal sealed partial class EngineWorker
{
    /// <summary>
    /// The hook decided from its shadow, which follows the machine's rules; a mismatch on the hold key's own press or release,
    /// on a button the hold remap names, or on a move swallowed for a drag (the first of a hold: the rest are the same
    /// mismatch, at Debug) is a bug (Warning). Elsewhere (a wheel notch, another key or button) it can follow a reset the
    /// shadow did not share (dropped events), so it is logged at Debug.
    /// </summary>
    private void CrossCheckHold(HoldRemapEvent e, bool hookSuppressed, IReadOnlyList<HoldRemapOutcome> outcomes, HoldRemapEntry? entry)
    {
        // For a focus move the hook's "decision" is that it ended the hold; the machine must have ended it too.
        var machineSuppressed = e is HoldRemapEvent.FocusMoved
            ? outcomes.Any(outcome => outcome is HoldRemapOutcome.HoldEnded)
            : outcomes.Count > 0 && outcomes[0] is HoldRemapOutcome.Suppress;
        if (e is HoldRemapEvent.Reset || machineSuppressed == hookSuppressed)
        {
            return;
        }

        var own = e is HoldRemapEvent.HoldDown or HoldRemapEvent.HoldUp or HoldRemapEvent.FocusMoved
            || (e is HoldRemapEvent.Button button && entry?.IsInput(button.MouseButton) == true)
            || (e is HoldRemapEvent.Key key && key.KeyCode == entry?.HoldKey);
        if (e is HoldRemapEvent.Move)
        {
            // Every move of the drag would mismatch alike: one Warning until the next reset or hold, the rest at Debug.
            own = !_moveMismatchWarned;
            _moveMismatchWarned = true;
        }

        var level = own ? EventLevel.Warning : EventLevel.Debug;
        if (_log.IsEnabled(level))
        {
            _log.Log(new LogEvent(DateTimeOffset.Now, level, LogSources.Hold, "Hold decision mismatch", [new("event", e.ToString()), new("hook", hookSuppressed), new("machine", machineSuppressed)]));
        }
    }

    /// <summary>Debug lines per hold: its start, its tap or why not, its end when focus moved, a rollover, every output pressed and released.</summary>
    private void LogHold(HoldRemapEvent e, HoldRemapState before, IReadOnlyList<HoldRemapOutcome> outcomes, HoldRemapEntry? entry, string? app)
    {
        var started = e is HoldRemapEvent.HoldDown && before is HoldRemapState.Idle or HoldRemapState.Following && _holdMachine.State == HoldRemapState.Holding;
        if (started)
        {
            _holdDownAt = e.TimestampMs;
            _moveMismatchWarned = false;
        }

        if (!_log.IsEnabled(EventLevel.Debug))
        {
            return;
        }

        var tapped = false;
        foreach (var outcome in outcomes)
        {
            tapped |= outcome is HoldRemapOutcome.TapHoldKey;
        }

        if (started)
        {
            _log.Debug(LogSources.Hold, "Hold", ("key", KeyName(entry!.HoldKey)), ("remap", entry.Name), ("app", app));
        }
        else if (e is HoldRemapEvent.HoldUp up && before is HoldRemapState.Holding or HoldRemapState.RolledOver && entry is not null && up.HoldKey == entry.HoldKey)
        {
            var heldMs = up.TimestampMs - _holdDownAt;
            if (tapped)
            {
                _log.Debug(LogSources.Hold, "Tap sent", ("key", KeyName(up.HoldKey)), ("heldMs", heldMs));
            }
            else
            {
                var reason = before == HoldRemapState.RolledOver ? "sent at the rollover"
                    : heldMs > entry.TapTimeMs ? $"held longer than the tap time ({entry.TapTimeMs} ms)"
                    : "a mouse button, a wheel turn or an input was used";
                _log.Debug(LogSources.Hold, "Tap not sent", ("key", KeyName(up.HoldKey)), ("heldMs", heldMs), ("reason", reason));
            }
        }

        foreach (var outcome in outcomes)
        {
            switch (outcome)
            {
                case HoldRemapOutcome.HoldEnded ended:
                    _log.Debug(LogSources.Hold, "Hold ended: focus moved", ("key", KeyName(ended.HoldKey)), ("heldMs", e.TimestampMs - _holdDownAt));
                    break;
                case HoldRemapOutcome.ReplayKey { Phase: KeyPhase.Down } replay when tapped:
                    _log.Debug(LogSources.Hold, "Rollover", ("key", KeyName(replay.Key)), ("holdKey", KeyName(entry?.HoldKey ?? KeyCode.None)));
                    break;
                case HoldRemapOutcome.PressOutput press:
                    _log.Debug(LogSources.Hold, "Output pressed", ("output", press.Output.Describe(HotkeyText.Names)), ("command", CommandName(entry, press.CommandId)), ("x", press.X), ("y", press.Y));
                    break;
                case HoldRemapOutcome.ReleaseOutput { Output: RemapOutput.Button } release when _gate.RepostsRemapDrags:
                    _log.Debug(LogSources.Hold, "Output released", ("output", release.Output.Describe(HotkeyText.Names)), ("command", CommandName(entry, release.CommandId)), ("drags", _releasedDrags.Drags), ("dragsFailed", _releasedDrags.Failures));
                    break;
                case HoldRemapOutcome.ReleaseOutput release:
                    _log.Debug(LogSources.Hold, "Output released", ("output", release.Output.Describe(HotkeyText.Names)), ("command", CommandName(entry, release.CommandId)));
                    break;
                case HoldRemapOutcome.WheelOutput wheel:
                    _log.Debug(LogSources.Hold, "Output turned", ("output", wheel.Output.Describe(HotkeyText.Names)), ("command", CommandName(entry, wheel.CommandId)));
                    break;
            }
        }
    }

    private static string KeyName(KeyCode key) => HotkeyText.KeyName(key);

    private static string? CommandName(HoldRemapEntry? entry, CommandId id) => entry?.FindBinding(id)?.Name;

    private static string Describe(HoldRemapOutcome outcome) => outcome switch
    {
        HoldRemapOutcome.TapHoldKey tap => $"tap {KeyName(tap.Key)}",
        HoldRemapOutcome.PressOutput press => $"press {press.Output.Describe(HotkeyText.Names)}",
        HoldRemapOutcome.RepeatOutput repeat => $"repeat {repeat.Output.Describe(HotkeyText.Names)}",
        HoldRemapOutcome.ReleaseOutput release => $"release {release.Output.Describe(HotkeyText.Names)}",
        HoldRemapOutcome.DragOutput drag => $"drag {drag.Output.Describe(HotkeyText.Names)}",
        HoldRemapOutcome.WheelOutput wheel => $"turn {wheel.Output.Describe(HotkeyText.Names)}",
        HoldRemapOutcome.ReplayKey replay => $"replay {KeyName(replay.Key)} {replay.Phase.ToString().ToLowerInvariant()}",
        _ => outcome.GetType().Name,
    };
}
