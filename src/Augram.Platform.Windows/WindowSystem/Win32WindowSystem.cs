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

    /// <summary>
    /// A20 over top-level windows: the one under the gesture start (<c>GA_ROOT</c> of the handle; the root owner when the
    /// handle is gone) against the one in front. The activator then brings that top-level window forward, so a gesture over
    /// an owned dialog activates the dialog, not its owner.
    /// </summary>
    public ActivationResult Activate(WindowIdentity target)
    {
        var foregroundTopLevel = _win.RootOrSelf(_foreground.ForegroundWindow());
        var targetTopLevel = WindowHandles.Or(_win.Root(target.Handle), target.RootHandle);
        return ActivationPolicy.NeedsActivation(target, targetTopLevel, foregroundTopLevel, _win.IsEnabled(targetTopLevel))
            ? _activator.Activate(target, targetTopLevel)
            : ActivationResult.NotNeeded;
    }
}
