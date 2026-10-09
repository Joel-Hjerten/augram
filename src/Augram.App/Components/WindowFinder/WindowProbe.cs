using Augram.Core.Abstractions;

namespace Augram.App.Components.WindowFinder;

/// <summary>
/// What a <see cref="WindowFinder"/> asks <see cref="IWindowSystem"/> while it is dragged and when it is released. Points are
/// the window system's screen units: physical pixels on Windows, points on macOS (the finder converts the pointer).
/// <see cref="Hover"/> runs on every pointer move, so it asks the cheap <see cref="IWindowSystem.WindowKeyAt"/> first and reads
/// an identity only when the key changes (0 is no window); a platform without a cheap key (null) is asked at most once per
/// <see cref="UnkeyedInterval"/>. <see cref="Pick"/> reads the window at the release point afresh. A window of this process
/// (Augram's own settings window or a dialog) is never a pick: <see cref="IsOwn"/>.
/// </summary>
internal sealed class WindowProbe
{
    public static readonly TimeSpan UnkeyedInterval = TimeSpan.FromMilliseconds(50);

    private readonly IWindowSystem _windows;
    private readonly int _ownProcessId;
    private readonly Func<long> _nowMs;
    private nint _key = -1;
    private long? _askedAtMs;
    private WindowIdentity? _window;

    public WindowProbe(IWindowSystem windows, int ownProcessId, Func<long>? nowMs = null)
    {
        ArgumentNullException.ThrowIfNull(windows);
        _windows = windows;
        _ownProcessId = ownProcessId;
        _nowMs = nowMs ?? (() => Environment.TickCount64);
    }

    /// <summary>The window under the point while dragging, own windows included (the finder says so); null over nothing.</summary>
    public WindowIdentity? Hover(int x, int y)
    {
        var key = _windows.WindowKeyAt(x, y);
        if (key is { } known)
        {
            if (known != _key)
            {
                _key = known;
                _window = known == 0 ? null : _windows.WindowAt(x, y);
            }

            return _window;
        }

        var now = _nowMs();
        if (_askedAtMs is not { } asked || now - asked >= (long)UnkeyedInterval.TotalMilliseconds)
        {
            _askedAtMs = now;
            _window = _windows.WindowAt(x, y);
        }

        return _window;
    }

    /// <summary>The window to pick at the release point: null over nothing and over Augram's own windows.</summary>
    public WindowIdentity? Pick(int x, int y)
    {
        var window = _windows.WindowAt(x, y);
        return window is null || IsOwn(window) ? null : window;
    }

    public bool IsOwn(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.ProcessId == _ownProcessId;
    }
}
