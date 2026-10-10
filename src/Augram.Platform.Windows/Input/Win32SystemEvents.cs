using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.Windows.Interop;
using Microsoft.Win32;

namespace Augram.Platform.Windows.Input;

/// <summary>
/// The <see cref="ISystemEvents"/> over <see cref="SystemEvents"/>: session lock/unlock, suspend/resume,
/// display settings and session end mapped to <see cref="SystemEventKind"/>. <see cref="SystemEvents"/>
/// delivers on the thread that first touched it when that thread is STA and pumps messages (the App
/// creates this on its UI thread, so that is where <see cref="Occurred"/> fires); otherwise it runs its
/// own hidden-window thread. Also a new foreground window (<see cref="SystemEventKind.ForegroundChanged"/>, hold remaps,
/// plan 0002 decision 5): <c>SetWinEventHook(EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT)</c> on the constructing thread,
/// whose message loop delivers it (the UI thread; a thread that never pumps installs it all the same and simply hears
/// nothing). Listeners must not block in either case. Dispose unsubscribes and unhooks.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32SystemEvents : ISystemEvents, IDisposable
{
    /// <summary>The WinEvent callback carries no state but its hook handle: each instance is found by it.</summary>
    private static readonly ConcurrentDictionary<nint, Win32SystemEvents> ByHook = new();

    private readonly nint _foregroundHook;
    private int _disposed;

    public Win32SystemEvents()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
        _foregroundHook = HookForeground();
        if (_foregroundHook != 0)
        {
            // Before any message is pumped on this thread, so no callback can miss it.
            ByHook[_foregroundHook] = this;
        }
    }

    public event EventHandler<SystemEventKind>? Occurred;

    /// <summary>True when the foreground WinEvent hook was installed (zero handle: no desktop, and only the 200 ms poll sees focus move).</summary>
    public bool WatchesForeground => _foregroundHook != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.SessionEnding -= OnSessionEnding;
        if (_foregroundHook != 0)
        {
            ByHook.TryRemove(_foregroundHook, out _);
            // Windows removes the hook when its thread ends, so a call from another thread failing at exit costs nothing.
            NativeMethods.UnhookWinEvent(_foregroundHook);
        }
    }

    /// <summary>The mapping, exposed so a test can check it without raising OS events.</summary>
    public static SystemEventKind? Map(SessionSwitchReason reason) => reason switch
    {
        SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect => SystemEventKind.SessionLocked,
        SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect => SystemEventKind.SessionUnlocked,
        SessionSwitchReason.SessionLogoff => SystemEventKind.SessionEnding,
        _ => null,
    };

    public static SystemEventKind? Map(PowerModes mode) => mode switch
    {
        PowerModes.Suspend => SystemEventKind.Suspending,
        PowerModes.Resume => SystemEventKind.Resumed,
        _ => null,
    };

    /// <summary>A WinEvent's kind: <c>EVENT_SYSTEM_FOREGROUND</c> is a new foreground window; nothing else is asked for.</summary>
    public static SystemEventKind? MapWinEvent(uint eventType) => eventType == NativeMethods.EventSystemForeground ? SystemEventKind.ForegroundChanged : null;

    private static unsafe nint HookForeground()
        => NativeMethods.SetWinEventHook(NativeMethods.EventSystemForeground, NativeMethods.EventSystemForeground, 0, &OnWinEvent, 0, 0, NativeMethods.WinEventOutOfContext);

    [UnmanagedCallersOnly]
    private static void OnWinEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint threadId, uint timeMs)
    {
        if (!ByHook.TryGetValue(hook, out var events))
        {
            return;
        }

        try
        {
            events.Raise(MapWinEvent(eventType));
        }
        catch (Exception)
        {
            // An exception must not unwind into user32; the next foreground change is heard, and the poll backs it up.
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) => Raise(Map(e.Reason));

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) => Raise(Map(e.Mode));

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Raise(SystemEventKind.DisplayChanged);

    private void OnSessionEnding(object sender, SessionEndingEventArgs e) => Raise(SystemEventKind.SessionEnding);

    private void Raise(SystemEventKind? kind)
    {
        if (kind is { } value)
        {
            Occurred?.Invoke(this, value);
        }
    }
}
