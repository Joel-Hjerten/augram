using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using SharpHook;
using SharpHook.Data;

namespace Augram.Engine.Input;

/// <summary>
/// The hook's two rate-limited Info lines for input it drops before the handler (<see cref="SharpHookInputSource"/>):
/// "Button ignored" with the raw number, at most once per <see cref="Interval"/> for each button (numbers past 15 share one
/// slot), simulated or not and direction, so a remapped mouse button that never reaches the engine (a vendor tool injecting it)
/// shows what it arrives as (on Windows; on macOS foreign presses and releases are taken, <see cref="ForeignInput.Take"/>, so the
/// "simulated" reason is left to unknown buttons and a source without a ledger); "Wheel ignored", once per interval and reason (simulated, horizontal, no rotation), so a scrolling
/// utility that re-posts every wheel event is visible. One throttle (<see cref="Due"/>) over two tables of last-logged times.
/// Hook thread only: one writer, no lock; nothing is allocated unless a line is written.
/// </summary>
internal sealed class IgnoredInputLog
{
    /// <summary>The minimum gap between two lines for the same slot.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private const int ButtonSlots = 16;

    private readonly IClock _clock;
    private readonly IEventLog _log;
    private readonly bool _ownCounted;

    // Last "Button ignored" time per (button, simulated, pressed); 0 = never.
    private readonly long[] _buttonLoggedAt = new long[ButtonSlots * 4];

    // Last "Wheel ignored" time per reason (simulated: ours, or any without an OwnInjections; horizontal; no rotation); 0 = never.
    private readonly long[] _wheelLoggedAt = new long[3];

    /// <param name="clock">The source's clock.</param>
    /// <param name="log">Where the lines go.</param>
    /// <param name="ownCounted">The source has an <see cref="OwnInjections"/>: a simulated vertical wheel event it drops is one of Augram's own Scroll notches ("own scroll"); without one every simulated wheel event is dropped ("simulated").</param>
    public IgnoredInputLog(IClock clock, IEventLog log, bool ownCounted)
    {
        _clock = clock;
        _log = log;
        _ownCounted = ownCounted;
    }

    /// <summary>A button press or release dropped before the handler, for <paramref name="reason"/>.</summary>
    public void Button(MouseHookEventArgs e, string reason)
    {
        var number = (int)e.Data.Button;
        var pressed = e.RawEvent.Type == EventType.MousePressed;
        var slot = ((Math.Min(number, ButtonSlots - 1) * 2) + (e.IsEventSimulated ? 1 : 0)) * 2 + (pressed ? 1 : 0);
        if (!Due(_buttonLoggedAt, slot))
        {
            return;
        }

        _log.Info(
            LogSources.Hook,
            "Button ignored",
            ("button", number),
            ("direction", pressed ? "down" : "up"),
            ("reason", reason),
            ("mask", $"0x{(ushort)e.RawEvent.Mask:X4}"),
            ("x", e.Data.X),
            ("y", e.Data.Y));
    }

    /// <summary>A wheel event dropped before the handler: horizontal, no rotation, or simulated.</summary>
    public void Wheel(MouseWheelHookEventArgs e)
    {
        var slot = e.Data.Direction != MouseWheelScrollDirection.Vertical ? 1 : e.Data.Rotation == 0 ? 2 : 0;
        if (!Due(_wheelLoggedAt, slot))
        {
            return;
        }

        _log.Info(
            LogSources.Hook,
            "Wheel ignored",
            ("reason", slot switch { 0 => _ownCounted ? "own scroll" : "simulated", 1 => "horizontal", _ => "no rotation" }),
            ("simulated", e.IsEventSimulated),
            ("rotation", e.Data.Rotation),
            ("delta", e.Data.Delta),
            ("type", e.Data.Type),
            ("x", e.Data.X),
            ("y", e.Data.Y));
    }

    /// <summary>True, and the slot stamped now, when its last line is at least <see cref="Interval"/> old (or there is none) and Info is on.</summary>
    private bool Due(long[] loggedAt, int slot)
    {
        var now = _clock.MonotonicMs;
        var last = loggedAt[slot];
        if ((last != 0 && now - last < (long)Interval.TotalMilliseconds) || !_log.IsEnabled(EventLevel.Info))
        {
            return false;
        }

        loggedAt[slot] = now == 0 ? 1 : now;
        return true;
    }
}
