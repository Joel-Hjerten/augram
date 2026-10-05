namespace Augram.Core.Abstractions;

/// <summary>
/// Session, power and display notifications (<c>SystemEvents</c> on Windows). Platform implements
/// it; the Engine's hook health monitor logs each one and restarts its silence clock, since after a
/// sleep or lock the OS owes no events. Raised on whatever thread the OS uses.
/// </summary>
public interface ISystemEvents
{
    event EventHandler<SystemEventKind>? Occurred;
}
