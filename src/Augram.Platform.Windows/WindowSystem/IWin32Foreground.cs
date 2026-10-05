namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// The Win32 calls <see cref="ForegroundActivator"/> needs, separated from the queries because they have side effects.
/// </summary>
internal interface IWin32Foreground
{
    nint ForegroundWindow();

    bool SetForegroundWindow(nint hwnd);

    bool AttachThreadInput(uint attachFrom, uint attachTo, bool attach);

    uint CurrentThreadId();

    uint WindowThreadId(nint hwnd);

    /// <summary>Presses and releases Alt through SendInput: the classic nudge that lifts the foreground lock.</summary>
    void SendAltTap();
}
