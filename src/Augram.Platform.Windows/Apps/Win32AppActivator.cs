using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.Windows.Interop;
using Augram.Platform.Windows.WindowSystem;

namespace Augram.Platform.Windows.Apps;

/// <summary>
/// The Windows <see cref="IAppActivator"/> (the Open app step, Joel 2026-10-09): walks the top-level windows front to back
/// and takes the first that is shown (or minimized), has no owner and is no tool window, and whose process is the
/// executable asked for (file name, any case; read like every window identity, so a UWP app counts as itself). A minimized
/// one is restored; then the foreground activator (B1) brings it forward. No such window → not running, and the step
/// launches the app. Runs on the command executor; it may sleep while activating.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32AppActivator : IAppActivator
{
    private readonly IWin32Windows _windows;
    private readonly IWin32WindowControl _control;
    private readonly WindowIdentityReader _reader;
    private readonly ForegroundActivator _activator;

    public Win32AppActivator(IEventLog log)
        : this(new Win32Windows(), log)
    {
    }

    private Win32AppActivator(Win32Windows win, IEventLog log)
        : this(win, win, win, log, null)
    {
    }

    internal Win32AppActivator(IWin32Windows windows, IWin32Foreground foreground, IWin32WindowControl control, IEventLog log, Action<int>? sleep)
    {
        _windows = windows;
        _control = control;
        _reader = new WindowIdentityReader(windows);
        _activator = new ForegroundActivator(foreground, windows, log, sleep);
    }

    public AppActivation BringToFront(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        var wanted = Path.GetFileName(executable.Trim());
        if (Find(wanted) is not { } window)
        {
            return AppActivation.NotRunning;
        }

        if (_windows.IsIconic(window.RootHandle))
        {
            _control.ShowWindow(window.RootHandle, NativeMethods.SwRestore);
        }

        return _activator.Activate(window).Succeeded
            ? AppActivation.Activated
            : AppActivation.Failed($"Windows did not bring {window.ProcessName} to the front");
    }

    private WindowIdentity? Find(string executable)
    {
        foreach (var hwnd in _windows.TopLevelWindows())
        {
            if ((!_windows.IsVisible(hwnd) && !_windows.IsIconic(hwnd)) || _windows.Owner(hwnd) != 0 || _windows.IsToolWindow(hwnd))
            {
                continue;
            }

            if (_reader.Read(hwnd) is { IsDesktop: false } identity
                && string.Equals(identity.ProcessName, executable, StringComparison.OrdinalIgnoreCase))
            {
                return identity;
            }
        }

        return null;
    }
}
