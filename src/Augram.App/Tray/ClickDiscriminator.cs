namespace Augram.App.Tray;

/// <summary>
/// Turns a stream of clicks into single and double clicks. Avalonia's <c>TrayIcon</c> raises only
/// <c>Clicked</c>, so F7's single-click-toggles / double-click-opens is reconstructed here: a click is
/// <see cref="ClickKind.Pending"/> until either a second click arrives inside <see cref="Window"/>
/// (<see cref="ClickKind.Double"/>) or <see cref="Flush"/> runs after it (<see cref="ClickKind.Single"/>).
/// Pure and clock-driven so the timing is testable; the tray owns the timer.
/// </summary>
public sealed class ClickDiscriminator
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMilliseconds(250);

    private DateTimeOffset? _pending;

    public ClickDiscriminator(TimeSpan? window = null)
    {
        Window = window ?? DefaultWindow;
    }

    public TimeSpan Window { get; }

    public ClickKind Click(DateTimeOffset now)
    {
        if (_pending is { } first && now - first <= Window)
        {
            _pending = null;
            return ClickKind.Double;
        }

        _pending = now;
        return ClickKind.Pending;
    }

    public ClickKind Flush(DateTimeOffset now)
    {
        if (_pending is { } first && now - first > Window)
        {
            _pending = null;
            return ClickKind.Single;
        }

        return ClickKind.None;
    }
}
