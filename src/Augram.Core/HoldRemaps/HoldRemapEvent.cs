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

    /// <summary>One physical wheel notch at (<paramref name="X"/>, <paramref name="Y"/>).</summary>
    public sealed record Wheel(WheelDirection Direction, int X, int Y, long TimestampMs) : HoldRemapEvent(TimestampMs);

    /// <summary>
    /// A physical key's down, auto-repeat or up (the hold key's own repeats included). (<paramref name="X"/>,
    /// <paramref name="Y"/>) is where the pointer is, for a button output pressed by a key input.
    /// </summary>
    public sealed record Key(KeyCode KeyCode, KeyPhase Phase, long TimestampMs, int X = 0, int Y = 0) : HoldRemapEvent(TimestampMs);

    /// <summary>The engine stops or the hook was reinstalled: release everything an output holds and forget the hold.</summary>
    public sealed record Reset(long TimestampMs) : HoldRemapEvent(TimestampMs);
}
