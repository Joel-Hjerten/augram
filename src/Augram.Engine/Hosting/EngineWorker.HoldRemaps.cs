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
/// itself, in order with the input (low latency, and nothing reaches the OS out of turn): a button output through
/// <see cref="IInputSimulator.PressRemapButton"/> (its modifiers around the down, at the event's position) and
/// <see cref="IInputSimulator.ReleaseRemapButton"/>; where the simulator re-posts drags (macOS), each move the hook swallowed
/// while it is held through <see cref="IInputSimulator.DragRemapButton"/>, with the delta from the previous physical position
/// seen here (a button event's or a move's); a key output with its modifiers held through the press (repeats mirrored); a
/// wheel output through <see cref="IInputSimulator.Scroll"/> (so <see cref="OwnInjections"/> claims it); the hold key's
/// tap; replayed keys. A Steps command goes to the <see cref="CommandExecutor"/>. Keys the hook swallowed only to keep the
/// order behind a rollover are replayed here as they were. A reset (hook reset, dropped events, engine stop) releases
/// everything still down (A19). Acts first, logs second.
/// </summary>
internal sealed partial class EngineWorker
{
    private readonly HoldRemapMachine _holdMachine = new();

    /// <summary>Keys replayed in order (<c>HoldReplay</c>) whose release the OS is still owed.</summary>
    private readonly HashSet<KeyCode> _orderedDown = [];

    private long _holdDownAt;

    /// <summary>The last physical position a hold message carried (a button event's or a move's): a re-posted drag's delta is from here.</summary>
    private int _holdX;
    private int _holdY;

    /// <summary>Drags re-posted, and how many failed, under the button output held now; reported with its release.</summary>
    private int _drags;
    private int _dragFailures;
    private (int Drags, int Failures) _releasedDrags;

    /// <summary>A swallowed move the machine did not re-post was warned about once since the last reset or hold.</summary>
    private bool _moveMismatchWarned;

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

            NotePosition(e, message.HookSuppressed, outcomes);
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
        _moveMismatchWarned = false;
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
            HoldRemapOutcome.DragOutput drag => Drag(drag),
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

    private SimulationResult Tap(KeyCode key) => InputSimulation.Worst(_simulator.KeyPress(key), _simulator.KeyRelease(key));

    /// <summary>A button output: its modifiers around the down only (Blender reads them when the drag starts), the simulator's own way; a key output: its modifiers held until its release.</summary>
    private SimulationResult PressOutput(RemapOutput output, int x, int y)
    {
        switch (output)
        {
            case RemapOutput.Button button:
                return _simulator.PressRemapButton(button.MouseButton, button.Modifiers & HotkeyKeys.AllModifiers, x, y);
            case RemapOutput.Key { IsSet: true } key:
                return InputSimulation.Worst(_simulator.PressKeys(ModifierKeysOf(key)), _simulator.KeyPress(key.KeyCode));
            default:
                return SimulationResult.Success;
        }
    }

    private SimulationResult ReleaseOutput(RemapOutput output)
    {
        switch (output)
        {
            case RemapOutput.Button button:
                _releasedDrags = (_drags, _dragFailures);
                _drags = 0;
                _dragFailures = 0;
                return _simulator.ReleaseRemapButton(button.MouseButton);
            case RemapOutput.Key { IsSet: true } key:
                return InputSimulation.Worst(_simulator.KeyRelease(key.KeyCode), _simulator.ReleaseKeys(ModifierKeysOf(key)));
            default:
                return SimulationResult.Success;
        }
    }

    /// <summary>
    /// A swallowed physical move re-posted as a drag of the button output held (macOS), with the delta from the previous
    /// physical position seen here. Counted; a failure is warned once per output (a move comes many times a second), the
    /// count goes with the output's release line.
    /// </summary>
    private SimulationResult Drag(HoldRemapOutcome.DragOutput drag)
    {
        var result = _simulator.DragRemapButton(drag.Output.MouseButton, drag.X, drag.Y, drag.X - _holdX, drag.Y - _holdY);
        _drags++;
        if (result == SimulationResult.Success)
        {
            return result;
        }

        return _dragFailures++ == 0 ? result : SimulationResult.Success;
    }

    /// <summary>
    /// After a button event or a move: the position the next drag's delta is taken from. A move the hook swallowed that the
    /// machine does not re-post (it reset without the hook: dropped events) still moves the pointer, so it never freezes.
    /// </summary>
    private void NotePosition(HoldRemapEvent e, bool hookSuppressed, IReadOnlyList<HoldRemapOutcome> outcomes)
    {
        switch (e)
        {
            case HoldRemapEvent.Button button:
                (_holdX, _holdY) = (button.X, button.Y);
                break;
            case HoldRemapEvent.Move move:
                (_holdX, _holdY) = (move.X, move.Y);
                if (hookSuppressed && outcomes[0] is HoldRemapOutcome.PassThrough)
                {
                    _simulator.MoveTo(move.X, move.Y);
                }

                break;
        }
    }

    /// <summary>One notch at the event's position with the output's modifiers held around it; <see cref="IInputSimulator.Scroll"/> announces it to <see cref="OwnInjections"/>, so no wheel trigger sees it.</summary>
    private SimulationResult TurnWheel(RemapOutput.Wheel wheel, int x, int y)
        => _simulator.WithModifiersHeld(HotkeyKeys.LeftKeys(wheel.Modifiers), () => _simulator.Scroll(wheel.Direction, 1, x, y));

    /// <summary>A Steps command: to the executor, target the foreground window; nothing to do without one (no mapping).</summary>
    private SimulationResult RunSteps(HoldRemapOutcome.RunSteps run, HoldRemapEntry? entry)
    {
        var binding = entry?.FindBinding(run.CommandId);
        if (_executor is null)
        {
            _log.Debug(LogSources.Hold, "Steps not run: no mapping", ("command", binding?.Name));
            return SimulationResult.Success;
        }

        var trigger = new PressedTrigger(binding is null ? Trigger.None : Trigger.ForInput(binding.Input), new PressHold(HeldButtons.None, _machine.StrokeButton));
        _executor.Enqueue(new ExecutionRequest(trigger, new CapturePoint(run.X, run.Y, _clock.MonotonicMs), null) { HoldCommand = run.CommandId, HoldRemapName = entry?.Name });
        return SimulationResult.Success;
    }

    /// <summary>A key output's modifier keys, left or right hand as it says: pressed before its key (<see cref="PressOutput"/>), released after it (<see cref="ReleaseOutput"/>).</summary>
    private static IReadOnlyList<KeyCode> ModifierKeysOf(RemapOutput.Key key) => HotkeyKeys.Keys(key.Modifiers, key.RightHand);
}
