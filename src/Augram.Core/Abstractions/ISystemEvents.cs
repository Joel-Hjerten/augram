namespace Augram.Core.Abstractions;

/// <summary>
/// Session, power and display notifications (<c>SystemEvents</c> on Windows). Platform implements
/// it; the Engine's hook health monitor logs each one and restarts its silence clock, since after a
/// sleep or lock the OS owes no events. Raised on whatever thread the OS uses.
/// </summary>
public interface ISystemEvents
{
    event EventHandler<SystemEventKind>? Occurred;

    /// <summary>
    /// Whether <see cref="SystemEventKind.ForegroundChanged"/> will be raised (Windows: the foreground WinEvent hook installed;
    /// macOS: the observers registered), so a new app in front reaches the engine's watch at once; false means only the watch's
    /// 200 ms poll sees focus move. Null when the source does not say. The engine logs it once at start.
    /// </summary>
    bool? ReportsForeground => null;
}
