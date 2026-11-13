using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Remap;

namespace Augram.Engine.Hosting;

/// <summary>
/// The drag trigger half of <see cref="EngineWorker"/> (plan 0015): "Right + drag" fired where a hand-back would have been, at the
/// drag distance. The press's <see cref="ButtonOutputs"/> (its window's, kept at the anchor's press by <c>NotePress</c>) say what
/// the drag does:
/// <list type="bullet">
/// <item>a Remap button output: held here, never through the executor (decision 3), pressed at the start point with its held key
/// and modifiers (<c>PressOutput</c>, the hold remaps' own), then dragged to where the pointer is now; released at the owner's
/// release, another program's release of it, a reset and engine stop, the held key last (decision 6). Where the simulator
/// re-posts (macOS: physical right-drags do not move a posted Left, decision 5) the gate swallows each move while it is held
/// (<see cref="InputGate.PublishRepostMoves"/>, set before the press) and <c>RepostMove</c> re-posts it as a drag of the output;</item>
/// <item>ordinary steps: enqueued once at the drag's start, as a button trigger's chord; nothing held, the owner stays swallowed;</item>
/// <item>no command (keys held that none names): the press goes back to the app as a hand-back does, its down at the start point,
/// the pointer moved here; its release injected at the owner's release, a reset or engine stop, and not after another program's
/// release (the OS has it up already).</item>
/// </list>
/// Exactly one release per press (A19). Acts first, logs second.
/// </summary>
internal sealed partial class EngineWorker
{
    private HeldDrag? _dragHeld;

    private void FireDrag(CaptureOutcome.DragTrigger drag)
    {
        var pressed = new PressedTrigger(new Trigger.DragTrigger(), drag.Hold);
        // The machine ends a drag before another starts; this only guards against a lost end.
        ReleaseDrag("another drag", ownerUpAlready: false);
        if (_pressOutputs.For(pressed) is { Output: RemapOutput.Button button } output)
        {
            _log.Info(LogSources.Capture, "Drag trigger", ("trigger", pressed.Describe()), ("x", drag.Start.X), ("y", drag.Start.Y));
            HoldDragOutput(drag, output, button, pressed);
            return;
        }

        if (_pressOutputs.RunsSteps(pressed))
        {
            _log.Info(LogSources.Capture, "Drag trigger", ("trigger", pressed.Describe()), ("x", drag.Start.X), ("y", drag.Start.Y));
            Fire(null, pressed, drag.Start, null, null);
            return;
        }

        // No command takes this drag here (the anchor is drag-bound for other keys): the app gets its drag, as a hand-back.
        _dragHeld = new HeldDrag(drag.Button, null, null);
        var down = _simulator.Press(drag.Button, drag.Start.X, drag.Start.Y);
        var moved = _simulator.MoveTo(drag.X, drag.Y);
        _log.Debug(LogSources.Capture, "Press handed back", ("button", drag.Button), ("x", drag.Start.X), ("y", drag.Start.Y), ("trigger", pressed.Describe()), ("result", down == SimulationResult.Success ? moved : down));
    }

    /// <summary>The output down at the start point (held key, modifiers around the down), then dragged to where the pointer is now.</summary>
    private void HoldDragOutput(CaptureOutcome.DragTrigger drag, ButtonOutput output, RemapOutput.Button button, PressedTrigger pressed)
    {
        var held = new HeldDrag(drag.Button, output, button);
        _dragHeld = held;
        // Before the press: no physical move may pass between the output's down and the hook swallowing moves (macOS).
        PublishRepostMoves();
        var result = PressOutput(button, drag.Start.X, drag.Start.Y);
        // The distance already moved: re-posted as one drag where physical moves do not drag it, else the pointer moved (Windows).
        var moved = _gate.RepostsRemapDrags ? DragHeld(held, drag.X, drag.Y, drag.X - drag.Start.X, drag.Y - drag.Start.Y) : _simulator.MoveTo(drag.X, drag.Y);
        result = InputSimulation.Worst(result, moved);
        if (result == SimulationResult.Success)
        {
            _log.Info(LogSources.Execution, "Output pressed", ("command", output.Name), ("output", button.Describe(HotkeyText.Names)), ("heldKey", button.HeldKey), ("trigger", pressed.Describe()), ("x", drag.Start.X), ("y", drag.Start.Y));
        }
        else
        {
            _log.Warning(LogSources.Execution, "Output failed", ("command", output.Name), ("output", button.Describe(HotkeyText.Names)), ("heldKey", button.HeldKey), ("result", result));
        }
    }

    /// <summary>The drag of <paramref name="ended"/>'s button is over: what it holds is released (after another program's release of the owner, a handed-back owner is not: the OS has it up).</summary>
    private void EndDrag(CaptureOutcome.DragTriggerEnded ended, CaptureEvent e)
    {
        if (_dragHeld is { } held && held.Owner == ended.Button)
        {
            ReleaseDrag(null, ownerUpAlready: e is CaptureEvent.ButtonReleasedElsewhere);
        }
    }

    /// <summary>
    /// Releases what a drag holds, if anything: the output's button, then its held key; or a handed-back owner's injected down
    /// unless <paramref name="ownerUpAlready"/>. <paramref name="reason"/> is logged when it is not the drag's own end.
    /// </summary>
    private void ReleaseDrag(string? reason, bool ownerUpAlready)
    {
        if (_dragHeld is not { } held)
        {
            return;
        }

        _dragHeld = null;
        if (held.Button is not { } button)
        {
            if (!ownerUpAlready)
            {
                var up = _simulator.Release(held.Owner);
                _log.Debug(LogSources.Capture, "Handed-back press released", ("button", held.Owner), ("result", up), ("reason", reason ?? "drag ended"));
            }

            return;
        }

        // Not ReleaseOutput: its drag counters are the hold remap's.
        var result = _simulator.ReleaseRemapButton(button.MouseButton, button.HeldKey);
        PublishRepostMoves();
        var (name, output) = (held.Output!.Name, button.Describe(HotkeyText.Names));
        if (result != SimulationResult.Success)
        {
            _log.Warning(LogSources.Execution, "Output release failed", ("command", name), ("output", output), ("heldKey", button.HeldKey), ("result", result));
            return;
        }

        if (_gate.RepostsRemapDrags)
        {
            _log.Info(LogSources.Execution, "Output released", ("command", name), ("output", output), ("heldKey", button.HeldKey), ("drags", held.Drags), ("dragsFailed", held.DragsFailed), ("reason", reason ?? "drag ended"));
        }
        else
        {
            _log.Info(LogSources.Execution, "Output released", ("command", name), ("output", output), ("heldKey", button.HeldKey), ("reason", reason ?? "drag ended"));
        }
    }

    /// <summary>
    /// One drag of the output held, at (<paramref name="x"/>, <paramref name="y"/>) by (<paramref name="dx"/>, <paramref name="dy"/>):
    /// counted, and a failure warned once per output (a move comes many times a second), as the hold remaps' drags are.
    /// </summary>
    private SimulationResult DragHeld(HeldDrag held, int x, int y, int dx, int dy)
    {
        var result = _simulator.DragRemapButton(held.Button!.MouseButton, x, y, dx, dy);
        held.Drags++;
        if (result != SimulationResult.Success && held.DragsFailed++ == 0)
        {
            _log.Warning(LogSources.Execution, "Drag re-post failed", ("command", held.Output!.Name), ("result", result));
        }

        return SimulationResult.Success;
    }

    /// <summary>Tells the gate whether moves are swallowed for re-posting now: a chord's key output with modifiers, or a drag's button output, is held.</summary>
    private void PublishRepostMoves() => _gate.PublishRepostMoves(_chordRepostsMoves || _dragHeld is { Button: not null });

    /// <summary>
    /// What a drag trigger holds from its start to the owner's release: the output (with its command) and how many moves were
    /// re-posted as its drags; or, with <see cref="Button"/> null, the owner handed back to the app.
    /// </summary>
    private sealed class HeldDrag(MouseButton owner, ButtonOutput? output, RemapOutput.Button? button)
    {
        public MouseButton Owner { get; } = owner;

        public ButtonOutput? Output { get; } = output;

        public RemapOutput.Button? Button { get; } = button;

        public int Drags { get; set; }

        public int DragsFailed { get; set; }
    }
}
