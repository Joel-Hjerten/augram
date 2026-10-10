using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Engine.Hosting;

/// <summary>
/// Initial engine settings and the tunables that are not user settings. The user-facing ones
/// (stroke button, thresholds, ignore key, enabled) can be changed later through <see cref="EngineHost"/>.
/// </summary>
/// <param name="StrokeButton">The button that, held, starts a gesture.</param>
/// <param name="Thresholds">Capture thresholds; null for <see cref="CaptureThresholds.Default"/>.</param>
/// <param name="IgnoreKey">Modifiers that make the stroke button pass through, read per press.</param>
/// <param name="Enabled">The tray toggle's initial value.</param>
/// <param name="TickInterval">How often the worker feeds a <c>Tick</c> to the state machine while a button is held; bounds the hold-still cancel's latency.</param>
/// <param name="QueueCapacity">Hook-to-worker channel size. Moves and ticks are dropped when full; a dropped button event is logged and resets the capture.</param>
/// <param name="HealthPollInterval">The hook watchdog's poll interval; null for the monitor's default (1 s).</param>
/// <param name="SettleDelayMs">A8: the wait between activating a target window that did not have focus and the first injected keystroke; never applied before a window operation, never applied when focus did not move.</param>
/// <param name="FocusPollInterval">How often the ignore-list watch asks what has focus while anything is watched (the backstop behind the platform's foreground notification); null for <see cref="IgnoreListWatch.FocusPollInterval"/> (200 ms).</param>
public sealed record EngineHostOptions(
    MouseButton StrokeButton = MouseButton.Right,
    CaptureThresholds? Thresholds = null,
    KeyModifiers IgnoreKey = KeyModifiers.None,
    bool Enabled = true,
    TimeSpan? TickInterval = null,
    int QueueCapacity = EngineHostOptions.DefaultQueueCapacity,
    TimeSpan? HealthPollInterval = null,
    int SettleDelayMs = EngineHostOptions.DefaultSettleDelayMs,
    TimeSpan? FocusPollInterval = null)
{
    public const int DefaultSettleDelayMs = 30;
    public const int DefaultQueueCapacity = 4096;
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromMilliseconds(25);

    public static EngineHostOptions Default { get; } = new();
}
