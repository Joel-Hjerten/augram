using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Microsoft.Win32;

namespace Augram.Platform.Windows.Input;

/// <summary>
/// The <see cref="ISystemEvents"/> over <see cref="SystemEvents"/>: session lock/unlock, suspend/resume,
/// display settings and session end mapped to <see cref="SystemEventKind"/>. <see cref="SystemEvents"/>
/// delivers on the thread that first touched it when that thread is STA and pumps messages (the App
/// creates this on its UI thread, so that is where <see cref="Occurred"/> fires); otherwise it runs its
/// own hidden-window thread. Listeners must not block in either case. Dispose unsubscribes.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Win32SystemEvents : ISystemEvents, IDisposable
{
    private int _disposed;

    public Win32SystemEvents()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
    }

    public event EventHandler<SystemEventKind>? Occurred;

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
