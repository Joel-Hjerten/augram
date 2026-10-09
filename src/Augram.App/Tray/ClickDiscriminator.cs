namespace Augram.App.Tray;

/// <summary>
/// Turns a stream of clicks into single and double clicks. Avalonia's <c>TrayIcon</c> raises only
/// <c>Clicked</c>, so F7's single-click-toggles / double-click-opens is reconstructed here: a click is
/// <see cref="ClickKind.Pending"/> until either a second click arrives inside <see cref="Window"/>
/// (<see cref="ClickKind.Double"/>) or <see cref="Flush"/> runs after it (<see cref="ClickKind.Single"/>).
/// Pure and clock-driven so the timing is testable; the tray owns the timer. A click within <see cref="Window"/> after a
/// double is swallowed (<see cref="ClickKind.None"/>): Windows can report a double click as three clicks (up, double, up),
/// and the third one used to start a single that toggled Augram after the window had opened.
/// </summary>
public sealed class ClickDiscriminator
{
    /// <summary>300 ms (Joel, 2026-10-09: the system's 500 ms made a single click feel slow; 250–300 is fine).</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMilliseconds(300);

    private DateTimeOffset? _pending;
    private DateTimeOffset? _doubleAt;

    public ClickDiscriminator(TimeSpan? window = null)
    {
        Window = window ?? DefaultWindow;
    }

    public TimeSpan Window { get; }

    public ClickKind Click(DateTimeOffset now)
    {
        if (_doubleAt is { } doubled && now - doubled <= Window)
        {
            return ClickKind.None;
        }

        if (_pending is { } first && now - first <= Window)
        {
            _pending = null;
            _doubleAt = now;
            return ClickKind.Double;
        }

        _pending = now;
        return ClickKind.Pending;
    }

    public ClickKind Flush(DateTimeOffset now)
    {
        // At the window, not after it: the tray's timer ticks once the window has passed, and a tick a hair early must not
        // wait a second full window (a single click then took twice as long).
        if (_pending is { } first && now - first >= Window)
        {
            _pending = null;
            return ClickKind.Single;
        }

        return ClickKind.None;
    }
}
