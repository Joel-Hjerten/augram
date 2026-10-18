using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Remap;

namespace Augram.Engine.Hosting;

/// <summary>
/// The button trigger half of <see cref="EngineWorker"/> (plan 0005): "Right + Left" fired at Left's press. A command among the
/// press's <see cref="ButtonOutputs"/> (its window's, handed over with the anchor's press) holds its key output here, pressed at
/// once and released when the chord ends, at a reset and at engine stop (decision 9: never through the executor, whose queue may
/// drop the release; A19: nothing stays held). Any other goes to the executor as a wheel trigger does, and runs at the press.
/// While a key output with modifiers is held the gate is told (<see cref="InputGate.PublishChordHoldsModifiers"/>): where the
/// simulator re-posts (macOS) the hook swallows each move and it is re-posted here through <see cref="IInputSimulator.RepostMove"/>,
/// with the delta from the previous physical position, so no hardware move resets the modifiers held.
/// </summary>
internal sealed partial class EngineWorker
{
    private ButtonOutputs _pressOutputs = ButtonOutputs.Empty;
    private HeldButtonOutput? _buttonHeld;

    /// <summary>A re-posted move failed under the output held now: warned once, as a drag is.</summary>
    private bool _repostFailed;

    /// <summary>A new press of an anchor started: the outputs of the window it went down over are the ones its chords use.</summary>
    private void NotePress(CaptureEvent e, object? payload)
    {
        if (e is CaptureEvent.ButtonDown down && _machine.State == CaptureState.Held && _machine.ActiveButton == down.Button)
        {
            _pressOutputs = payload as ButtonOutputs ?? ButtonOutputs.Empty;
        }
    }

    private void FireButton(CaptureOutcome.ButtonTrigger fired)
    {
        var pressed = new PressedTrigger(new Trigger.ButtonTrigger(fired.Button), fired.Hold);
        _log.Info(LogSources.Capture, "Button trigger", ("trigger", pressed.Describe()), ("x", fired.Start.X), ("y", fired.Start.Y));
        if (_pressOutputs.For(pressed) is not { } output)
        {
            Fire(null, pressed, fired.Start, null, null);
            return;
        }

        // The machine ends a chord before it fires another; this only guards a held key against a lost end.
        ReleaseButtonOutput("another chord");
        // Before the press: no move may pass between the modifiers going down and the hook knowing.
        _gate.PublishChordHoldsModifiers(output.Output is RemapOutput.Key { IsSet: true } key && (key.Modifiers & HotkeyKeys.AllModifiers) != 0);
        _repostFailed = false;
        var result = PressOutput(output.Output, fired.Start.X, fired.Start.Y);
        _buttonHeld = new HeldButtonOutput(fired.Button, output);
        if (result == SimulationResult.Success)
        {
            _log.Info(LogSources.Execution, "Button output held", ("command", output.Name), ("output", output.Output.Describe(HotkeyText.Names)), ("trigger", pressed.Describe()));
        }
        else
        {
            _log.Warning(LogSources.Execution, "Button output failed", ("command", output.Name), ("output", output.Output.Describe(HotkeyText.Names)), ("result", result));
        }
    }

    /// <summary>The chord of <paramref name="button"/> is over: the output it holds is released.</summary>
    private void EndButtonTrigger(MouseButton button)
    {
        if (_buttonHeld is { } held && held.Button == button)
        {
            ReleaseButtonOutput(null);
        }
    }

    /// <summary>Releases the output a chord holds, if any; <paramref name="reason"/> is logged when it is not the chord's own end.</summary>
    private void ReleaseButtonOutput(string? reason)
    {
        if (_buttonHeld is not { } held)
        {
            return;
        }

        _buttonHeld = null;
        var result = ReleaseOutput(held.Output.Output);
        _gate.PublishChordHoldsModifiers(false);
        if (result != SimulationResult.Success)
        {
            _log.Warning(LogSources.Execution, "Button output release failed", ("command", held.Output.Name), ("output", held.Output.Output.Describe(HotkeyText.Names)), ("result", result));
            return;
        }

        _log.Debug(LogSources.Execution, "Button output released", ("command", held.Output.Name), ("output", held.Output.Output.Describe(HotkeyText.Names)), ("reason", reason ?? "chord ended"));
    }

    /// <summary>
    /// A move the hook swallowed while a chord held a key output with modifiers, re-posted at its position with the delta from
    /// the previous physical one. Re-posted even when the chord has ended since (the hook decided first), so the pointer never freezes.
    /// </summary>
    private void RepostMove(CaptureEvent.Move move)
    {
        var result = _simulator.RepostMove(move.X, move.Y, move.X - _physicalX, move.Y - _physicalY);
        if (result != SimulationResult.Success && !_repostFailed)
        {
            _repostFailed = true;
            _log.Warning(LogSources.Execution, "Move re-post failed", ("result", result));
        }
    }

    private sealed record HeldButtonOutput(MouseButton Button, ButtonOutput Output);
}
