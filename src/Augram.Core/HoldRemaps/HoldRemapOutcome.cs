using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// What the <see cref="HoldRemapMachine"/> wants done after one event. A closed set. <see cref="Suppress"/> and
/// <see cref="PassThrough"/> are the input decision (exactly one for every button, wheel and key event, and for the hold
/// key's down and up), which the hook made already from its shadow; the worker compares. Every other outcome is work for
/// the worker, in order: inject, or run a command. Every <see cref="PressOutput"/> is followed by its
/// <see cref="ReleaseOutput"/>, at the input's release or at a <see cref="HoldRemapEvent.Reset"/> (A19).
/// </summary>
public abstract record HoldRemapOutcome
{
    private protected HoldRemapOutcome()
    {
    }

    /// <summary>Swallow the event: no app sees it.</summary>
    public sealed record Suppress : HoldRemapOutcome
    {
        public static Suppress Instance { get; } = new();
    }

    /// <summary>Let the event through untouched.</summary>
    public sealed record PassThrough : HoldRemapOutcome
    {
        public static PassThrough Instance { get; } = new();
    }

    /// <summary>The hold was a tap (or the user is typing): send <paramref name="Key"/> down and up.</summary>
    public sealed record TapHoldKey(KeyCode Key) : HoldRemapOutcome;

    /// <summary>
    /// Press <paramref name="Output"/> and keep it down: a button with its modifiers pressed around the down only, at
    /// (<paramref name="X"/>, <paramref name="Y"/>); a key with its modifiers held until <see cref="ReleaseOutput"/>.
    /// </summary>
    public sealed record PressOutput(CommandId CommandId, RemapOutput Output, int X, int Y) : HoldRemapOutcome;

    /// <summary>The input key auto-repeated: send <paramref name="Output"/>'s key down again (a key output only; its modifiers are still held).</summary>
    public sealed record RepeatOutput(CommandId CommandId, RemapOutput Output) : HoldRemapOutcome;

    /// <summary>Release what <see cref="PressOutput"/> pressed (a key output's modifiers with it).</summary>
    public sealed record ReleaseOutput(CommandId CommandId, RemapOutput Output) : HoldRemapOutcome;

    /// <summary>Turn the wheel one notch at (<paramref name="X"/>, <paramref name="Y"/>) with the output's modifiers held around it.</summary>
    public sealed record WheelOutput(CommandId CommandId, RemapOutput.Wheel Output, int X, int Y) : HoldRemapOutcome;

    /// <summary>
    /// The rollover: send the swallowed <paramref name="Key"/> event as it was (<paramref name="Phase"/>), after the hold
    /// key's tap. Its down, repeats and up are all replayed, so the app sees them in order after the space.
    /// </summary>
    public sealed record ReplayKey(KeyCode Key, KeyPhase Phase) : HoldRemapOutcome;

    /// <summary>Run the steps of the Steps command <paramref name="CommandId"/> (once per press, once per wheel notch) at (<paramref name="X"/>, <paramref name="Y"/>).</summary>
    public sealed record RunSteps(CommandId CommandId, int X, int Y) : HoldRemapOutcome;
}
