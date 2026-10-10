using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// One input event fed to the <see cref="HoldRemapMachine"/>, already translated by the engine. A closed set: the nested
/// records are the only cases. Timestamps are milliseconds on the engine's one monotonic clock; the machine never reads a
/// clock itself. The machine classifies buttons, wheel notches and keys against the hold remap of the hold in progress
/// itself (input or not, rollover or not), so it answers what the hook should have decided and the worker can compare.
/// </summary>
public abstract record HoldRemapEvent(long TimestampMs)
{
    /// <summary>
    /// The hook claimed a hold key's press for <paramref name="Remap"/> (the foreground app's plan at that moment, plan 0002
    /// decision 8): the hold starts, or resumes when the same hold remap still follows buttons from its last hold.
    /// </summary>
    public sealed record HoldDown(HoldRemapEntry Remap, long TimestampMs) : HoldRemapEvent(TimestampMs);

    /// <summary>
    /// The release of a hold key whose press was claimed (its up is always swallowed). Another key than the one held is
    /// handled as that key's release (decision 6: a second hold key during a hold is an ordinary key).
    /// </summary>
    public sealed record HoldUp(KeyCode HoldKey, long TimestampMs) : HoldRemapEvent(TimestampMs);

    /// <summary>A physical mouse button going down or up at (<paramref name="X"/>, <paramref name="Y"/>).</summary>
    public sealed record Button(MouseButton MouseButton, bool IsDown, int X, int Y, long TimestampMs) : HoldRemapEvent(TimestampMs);

    /// <summary>
    /// A physical pointer move (or drag) to (<paramref name="X"/>, <paramref name="Y"/>), fed only on a platform whose simulator
    /// re-posts drags (<see cref="IInputSimulator.RepostsRemapDrags"/>, macOS) and only while the hook says a button output is
    /// held: the machine answers whether it is, and which output the move is re-posted as a drag of.
    /// </summary>
    public sealed record Move(int X, int Y, long TimestampMs) : HoldRemapEvent(TimestampMs);

    /// <summary>One physical wheel notch at (<paramref name="X"/>, <paramref name="Y"/>).</summary>
    public sealed record Wheel(WheelDirection Direction, int X, int Y, long TimestampMs) : HoldRemapEvent(TimestampMs);

    /// <summary>
    /// A physical key's down, auto-repeat or up (the hold key's own repeats included). (<paramref name="X"/>,
    /// <paramref name="Y"/>) is where the pointer is, for a button output pressed by a key input.
    /// </summary>
    public sealed record Key(KeyCode KeyCode, KeyPhase Phase, long TimestampMs, int X = 0, int Y = 0) : HoldRemapEvent(TimestampMs);

    /// <summary>The engine stops or the hook was reinstalled: release everything an output holds and forget the hold.</summary>
    public sealed record Reset(long TimestampMs) : HoldRemapEvent(TimestampMs);

    /// <summary>
    /// The app in front is no longer the hold's (the published plan changed to another app group, or to none; the hook saw it
    /// at its next event): a hold with no input owed ends, without a tap; its hold key's repeats and release stay swallowed.
    /// A hold with inputs owed keeps following them (Joel, 2026-10-10, on a lost release leaving inputs remapped).
    /// </summary>
    public sealed record FocusMoved(long TimestampMs) : HoldRemapEvent(TimestampMs);
}
