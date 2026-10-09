using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.Windows.Interop;

namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// The Windows <see cref="IWindowSystem"/>: identity via <see cref="WindowIdentityReader"/>, activation via
/// <see cref="ForegroundActivator"/> gated by <see cref="ActivationPolicy"/> (A20). Every method may block for
/// milliseconds; the engine calls it from its worker, never from a hook handler.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32WindowSystem : IWindowSystem
{
    private readonly IWin32Windows _win;
    private readonly IWin32Foreground _foreground;
    private readonly WindowIdentityReader _reader;
    private readonly ForegroundActivator _activator;

    public Win32WindowSystem(IEventLog log)
        : this(new Win32Windows(), log)
    {
    }

    internal Win32WindowSystem(Win32Windows win, IEventLog log)
        : this(win, win, log, null)
    {
    }

    internal Win32WindowSystem(IWin32Windows win, IWin32Foreground foreground, IEventLog log, Action<int>? sleep)
    {
        _win = win;
        _foreground = foreground;
        _reader = new WindowIdentityReader(win);
        _activator = new ForegroundActivator(foreground, win, log, sleep);
    }

    public WindowIdentity? WindowAt(int x, int y) => _reader.Read(_win.WindowFromPoint(x, y));

    public WindowIdentity? Foreground() => _reader.Read(_foreground.ForegroundWindow());

    /// <summary><c>WindowFromPoint</c> alone: the handle is the key, so moving across one control asks nothing more.</summary>
    public nint? WindowKeyAt(int x, int y) => _win.WindowFromPoint(x, y);

    /// <summary><c>GetForegroundWindow</c> alone.</summary>
    public nint? ForegroundKey() => _foreground.ForegroundWindow();

    public ActivationResult Activate(WindowIdentity target)
    {
        var foreground = _foreground.ForegroundWindow();
        var foregroundRoot = foreground == 0 ? 0 : OrSelf(_win.RootOwner(foreground), foreground);
        return ActivationPolicy.NeedsActivation(target, foregroundRoot)
            ? _activator.Activate(target)
            : ActivationResult.NotNeeded;
    }

    private static nint OrSelf(nint value, nint fallback) => value == 0 ? fallback : value;
}
