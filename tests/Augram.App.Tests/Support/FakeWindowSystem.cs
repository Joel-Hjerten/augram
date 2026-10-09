using Augram.Core.Abstractions;

namespace Augram.App.Tests.Support;

/// <summary>
/// A scripted <see cref="IWindowSystem"/> for the window finder: windows as screen rectangles, the first one containing a
/// point wins (front to back); a window's key is its handle. It counts the identity reads and remembers the points asked.
/// It reads no real window and activates nothing.
/// </summary>
internal sealed class FakeWindowSystem : IWindowSystem
{
    private readonly List<(int Left, int Top, int Width, int Height, WindowIdentity Window)> _windows = [];

    /// <summary>False plays a platform without a cheap key: <see cref="WindowKeyAt"/> answers null.</summary>
    public bool HasKeys { get; init; } = true;

    public int WindowAtCalls { get; private set; }

    public List<(int X, int Y)> Asked { get; } = [];

    /// <summary>Another app's window as the platform would report it.</summary>
    public static WindowIdentity Window(string process, string? title = null, string? path = null, IReadOnlyList<string>? classes = null, nint handle = 0x100, int processId = 4242)
        => new(handle, handle, process, path, title, classes ?? [], processId, IsFullScreen: false, IsDesktop: false);

    /// <summary>A window of the test process itself, as Augram's own settings window reads.</summary>
    public static WindowIdentity OwnWindow() => Window("testhost.exe", "Augram", handle: 0x900, processId: Environment.ProcessId);

    /// <summary>Puts <paramref name="window"/> on screen, centred on <paramref name="centreX"/>, <paramref name="centreY"/>, <paramref name="size"/> wide and high.</summary>
    public FakeWindowSystem Around(int centreX, int centreY, WindowIdentity window, int size = 100)
    {
        _windows.Add((centreX - size / 2, centreY - size / 2, size, size, window));
        return this;
    }

    public WindowIdentity? WindowAt(int x, int y)
    {
        WindowAtCalls++;
        Asked.Add((x, y));
        return Find(x, y);
    }

    public nint? WindowKeyAt(int x, int y) => HasKeys ? Find(x, y)?.Handle ?? 0 : null;

    public WindowIdentity? Foreground() => null;

    public nint? ForegroundKey() => 0;

    public ActivationResult Activate(WindowIdentity target) => ActivationResult.NotNeeded;

    private WindowIdentity? Find(int x, int y)
    {
        foreach (var (left, top, width, height, window) in _windows)
        {
            if (x >= left && y >= top && x < left + width && y < top + height)
            {
                return window;
            }
        }

        return null;
    }
}
