using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;

namespace Augram.Engine.Hosting;

/// <summary>
/// The button trigger half of <see cref="EngineWorker"/> (plan 0005): "Right + Left" fired at Left's press. A command among the
/// press's <see cref="ButtonOutputs"/> (its window's, handed over with the anchor's press) holds its key output here, pressed at
/// once and released when the chord ends, at a reset and at engine stop (decision 9: never through the executor, whose queue may
/// drop the release; A19: nothing stays held). Any other goes to the executor as a wheel trigger does, and runs at the press.
/// </summary>
internal sealed partial class EngineWorker
{
    private ButtonOutputs _pressOutputs = ButtonOutputs.Empty;
    private HeldButtonOutput? _buttonHeld;

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
        if (result != SimulationResult.Success)
        {
            _log.Warning(LogSources.Execution, "Button output release failed", ("command", held.Output.Name), ("output", held.Output.Output.Describe(HotkeyText.Names)), ("result", result));
            return;
        }

        _log.Debug(LogSources.Execution, "Button output released", ("command", held.Output.Name), ("output", held.Output.Output.Describe(HotkeyText.Names)), ("reason", reason ?? "chord ended"));
    }

    private sealed record HeldButtonOutput(MouseButton Button, ButtonOutput Output);
}
