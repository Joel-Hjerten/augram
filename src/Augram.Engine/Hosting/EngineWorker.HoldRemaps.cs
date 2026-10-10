using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Remap;
using Augram.Engine.Execution;
using Augram.Engine.Input;

namespace Augram.Engine.Hosting;

/// <summary>
/// The hold remap half of <see cref="EngineWorker"/> (F9, plan 0002): runs the <see cref="HoldRemapMachine"/> on every
/// <c>Hold</c> message as the capture machine is run, cross-checks the hook's decision against it, and acts on its outcomes
/// itself, in order with the input (low latency, and nothing reaches the OS out of turn): a button output as its modifiers
/// down, <see cref="IInputSimulator.Press"/> at the event's position, modifiers up; a key output with its modifiers held
/// through the press (repeats mirrored); a wheel output through <see cref="IInputSimulator.Scroll"/> (so
/// <see cref="OwnWheelInjections"/> claims it); the hold key's tap; replayed keys. A Steps command goes to the
/// <see cref="CommandExecutor"/>. Keys the hook swallowed only to keep the order behind a rollover are replayed here as they
/// were. A reset (hook reset, dropped events, engine stop) releases everything still down (A19). Acts first, logs second.
/// </summary>
internal sealed partial class EngineWorker
{
    private readonly HoldRemapMachine _holdMachine = new();

    /// <summary>Keys replayed in order (<c>HoldReplay</c>) whose release the OS is still owed.</summary>
    private readonly HashSet<KeyCode> _orderedDown = [];

    private long _holdDownAt;

    private void OnHold(WorkerMessage message)
    {
        try
        {
            var e = (HoldRemapEvent)message.Payload!;
            var before = _holdMachine.State;
            // The hold remap of this event, for command names and the Steps request (the machine may forget it after a release).
            var entry = e is HoldRemapEvent.HoldDown down ? down.Remap : _holdMachine.Remap;
            var outcomes = _holdMachine.Handle(e);
            foreach (var outcome in outcomes)
            {
                Act(outcome, entry);
            }

            CrossCheckHold(e, message.HookSuppressed, outcomes, entry);
            LogHold(e, before, outcomes, entry, message.App);
        }
        finally
        {
            if (message.Ordered)
            {
                _gate.HoldReplayDone();
            }
        }
    }

    /// <summary>A key the hook swallowed only to keep it behind a rollover's replays: replayed as it was, its release paired (A19).</summary>
    private void OnHoldReplay(WorkerMessage message)
    {
        try
        {
            var key = (HoldRemapEvent.Key)message.Payload!;
            SimulationResult result;
            if (key.Phase == KeyPhase.Up)
            {
                if (!_orderedDown.Remove(key.KeyCode))
                {
                    // Released already, at a reset.
                    return;
                }

                result = _simulator.KeyRelease(key.KeyCode);
            }
            else
            {
                _orderedDown.Add(key.KeyCode);
                result = _simulator.KeyPress(key.KeyCode);
            }

            _log.Debug(LogSources.Hold, "Key replayed in order", ("key", key.KeyCode), ("phase", key.Phase), ("result", result));
        }
        finally
        {
            _gate.HoldReplayDone();
        }
    }

    /// <summary>
    /// The hold remap's hard reset: every output it holds released, every replayed key's up sent, every key replayed in order
    /// released (A19), the machine idle. The hook's shadow is reset by the host when the hook was (unlock, resume, reinstall).
    /// </summary>
    private void ResetHold(string reason)
    {
        var entry = _holdMachine.Remap;
        var outcomes = _holdMachine.Handle(new HoldRemapEvent.Reset(_clock.MonotonicMs));
        foreach (var outcome in outcomes)
        {
            Act(outcome, entry);
        }

        foreach (var key in _orderedDown)
        {
            _simulator.KeyRelease(key);
        }

        var released = outcomes.Count + _orderedDown.Count;
        _orderedDown.Clear();
        if (released > 0)
        {
            _log.Debug(LogSources.Hold, "Hold remap reset; everything held released", ("reason", reason), ("released", released));
        }
    }

    private void Act(HoldRemapOutcome outcome, HoldRemapEntry? entry)
    {
        var result = outcome switch
        {
            HoldRemapOutcome.TapHoldKey tap => Tap(tap.Key),
            HoldRemapOutcome.PressOutput press => PressOutput(press.Output, press.X, press.Y),
            HoldRemapOutcome.RepeatOutput { Output: RemapOutput.Key { IsSet: true } key } => _simulator.KeyPress(key.KeyCode),
            HoldRemapOutcome.ReleaseOutput release => ReleaseOutput(release.Output),
            HoldRemapOutcome.WheelOutput wheel => TurnWheel(wheel.Output, wheel.X, wheel.Y),
            HoldRemapOutcome.ReplayKey { Phase: KeyPhase.Up } replay => _simulator.KeyRelease(replay.Key),
            HoldRemapOutcome.ReplayKey replay => _simulator.KeyPress(replay.Key),
            HoldRemapOutcome.RunSteps run => RunSteps(run, entry),
            _ => SimulationResult.Success,
        };

        if (result != SimulationResult.Success)
        {
            _log.Warning(LogSources.Hold, "Hold remap injection failed", ("what", Describe(outcome)), ("result", result));
        }
    }

    private SimulationResult Tap(KeyCode key) => Worst(_simulator.KeyPress(key), _simulator.KeyRelease(key));

    /// <summary>A button output: its modifiers around the down only (Blender reads them when the drag starts); a key output: its modifiers held until its release.</summary>
    private SimulationResult PressOutput(RemapOutput output, int x, int y)
    {
        switch (output)
        {
            case RemapOutput.Button button:
                var keys = SharpHookInputSimulator.ModifierKeys(button.Modifiers & HotkeyKeys.AllModifiers, KeyModifiers.None);
                var pressed = PressKeys(keys);
                pressed = Worst(pressed, _simulator.Press(button.MouseButton, x, y));
                return Worst(pressed, ReleaseKeys(keys));
            case RemapOutput.Key { IsSet: true } key:
                return Worst(PressKeys(ModifierKeysOf(key)), _simulator.KeyPress(key.KeyCode));
            default:
                return SimulationResult.Success;
        }
    }

    private SimulationResult ReleaseOutput(RemapOutput output) => output switch
    {
        RemapOutput.Button button => _simulator.Release(button.MouseButton),
        RemapOutput.Key { IsSet: true } key => Worst(_simulator.KeyRelease(key.KeyCode), ReleaseKeys(ModifierKeysOf(key))),
        _ => SimulationResult.Success,
    };

    /// <summary>One notch at the event's position with the output's modifiers held around it; <see cref="IInputSimulator.Scroll"/> announces it to <see cref="OwnWheelInjections"/>, so no wheel trigger sees it.</summary>
    private SimulationResult TurnWheel(RemapOutput.Wheel wheel, int x, int y)
    {
        var keys = SharpHookInputSimulator.ModifierKeys(wheel.Modifiers & HotkeyKeys.AllModifiers, KeyModifiers.None);
        var result = PressKeys(keys);
        result = Worst(result, _simulator.Scroll(wheel.Direction, 1, x, y));
        return Worst(result, ReleaseKeys(keys));
    }

    /// <summary>A Steps command: to the executor, target the foreground window; nothing to do without one (no mapping).</summary>
    private SimulationResult RunSteps(HoldRemapOutcome.RunSteps run, HoldRemapEntry? entry)
    {
        HoldBinding? binding = null;
        foreach (var candidate in entry?.Bindings ?? [])
        {
            if (candidate.CommandId == run.CommandId)
            {
                binding = candidate;
                break;
            }
        }

        if (_executor is null)
        {
            _log.Debug(LogSources.Hold, "Steps not run: no mapping", ("command", binding?.Name));
            return SimulationResult.Success;
        }

        var trigger = new PressedTrigger(binding is null ? Trigger.None : Trigger.ForInput(binding.Input), new PressHold(HeldButtons.None, _machine.StrokeButton));
        _executor.Enqueue(new ExecutionRequest(trigger, new CapturePoint(run.X, run.Y, _clock.MonotonicMs), null) { HoldCommand = run.CommandId, HoldRemapName = entry?.Name });
        return SimulationResult.Success;
    }

    private SimulationResult PressKeys(List<KeyCode> keys)
    {
        var result = SimulationResult.Success;
        foreach (var key in keys)
        {
            result = Worst(result, _simulator.KeyPress(key));
        }

        return result;
    }

    private SimulationResult ReleaseKeys(List<KeyCode> keys)
    {
        var result = SimulationResult.Success;
        for (var i = keys.Count - 1; i >= 0; i--)
        {
            result = Worst(result, _simulator.KeyRelease(keys[i]));
        }

        return result;
    }

    private static List<KeyCode> ModifierKeysOf(RemapOutput.Key key)
        => SharpHookInputSimulator.ModifierKeys(key.Modifiers & HotkeyKeys.AllModifiers, key.RightHand);

    private static SimulationResult Worst(SimulationResult a, SimulationResult b) => a >= b ? a : b;

    /// <summary>
    /// The hook decided from its shadow, which follows the machine's rules; a mismatch on the hold key's own press or release,
    /// or on a button the hold remap names, is a bug (Warning). Elsewhere (a wheel notch, another key or button) it can follow
    /// a reset the shadow did not share (dropped events), so it is logged at Debug.
    /// </summary>
    private void CrossCheckHold(HoldRemapEvent e, bool hookSuppressed, IReadOnlyList<HoldRemapOutcome> outcomes, HoldRemapEntry? entry)
    {
        var machineSuppressed = outcomes.Count > 0 && outcomes[0] is HoldRemapOutcome.Suppress;
        if (e is HoldRemapEvent.Reset || machineSuppressed == hookSuppressed)
        {
            return;
        }

        var own = e is HoldRemapEvent.HoldDown or HoldRemapEvent.HoldUp
            || (e is HoldRemapEvent.Button button && entry?.IsInput(button.MouseButton) == true)
            || (e is HoldRemapEvent.Key key && key.KeyCode == entry?.HoldKey);
        var level = own ? EventLevel.Warning : EventLevel.Debug;
        if (_log.IsEnabled(level))
        {
            _log.Log(new LogEvent(DateTimeOffset.Now, level, LogSources.Hold, "Hold decision mismatch", [new("event", e.ToString()), new("hook", hookSuppressed), new("machine", machineSuppressed)]));
        }
    }

    /// <summary>Debug lines per hold: its start, its tap or why not, a rollover, every output pressed and released.</summary>
    private void LogHold(HoldRemapEvent e, HoldRemapState before, IReadOnlyList<HoldRemapOutcome> outcomes, HoldRemapEntry? entry, string? app)
    {
        var started = e is HoldRemapEvent.HoldDown && before is HoldRemapState.Idle or HoldRemapState.Following && _holdMachine.State == HoldRemapState.Holding;
        if (started)
        {
            _holdDownAt = e.TimestampMs;
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
                case HoldRemapOutcome.ReplayKey { Phase: KeyPhase.Down } replay when tapped:
                    _log.Debug(LogSources.Hold, "Rollover", ("key", KeyName(replay.Key)), ("holdKey", KeyName(entry?.HoldKey ?? KeyCode.None)));
                    break;
                case HoldRemapOutcome.PressOutput press:
                    _log.Debug(LogSources.Hold, "Output pressed", ("output", press.Output.Describe(HotkeyText.Names)), ("command", CommandName(entry, press.CommandId)), ("x", press.X), ("y", press.Y));
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

    private static string? CommandName(HoldRemapEntry? entry, CommandId id)
    {
        foreach (var binding in entry?.Bindings ?? [])
        {
            if (binding.CommandId == id)
            {
                return binding.Name;
            }
        }

        return null;
    }

    private static string Describe(HoldRemapOutcome outcome) => outcome switch
    {
        HoldRemapOutcome.TapHoldKey tap => $"tap {KeyName(tap.Key)}",
        HoldRemapOutcome.PressOutput press => $"press {press.Output.Describe(HotkeyText.Names)}",
        HoldRemapOutcome.RepeatOutput repeat => $"repeat {repeat.Output.Describe(HotkeyText.Names)}",
        HoldRemapOutcome.ReleaseOutput release => $"release {release.Output.Describe(HotkeyText.Names)}",
        HoldRemapOutcome.WheelOutput wheel => $"turn {wheel.Output.Describe(HotkeyText.Names)}",
        HoldRemapOutcome.ReplayKey replay => $"replay {KeyName(replay.Key)} {replay.Phase.ToString().ToLowerInvariant()}",
        _ => outcome.GetType().Name,
    };
}
