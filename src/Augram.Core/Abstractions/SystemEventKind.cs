namespace Augram.Core.Abstractions;

/// <summary>OS events that can invalidate a global hook or the capture state (N4: session switch, power, display).</summary>
public enum SystemEventKind
{
    SessionLocked,
    SessionUnlocked,
    Suspending,
    Resumed,
    DisplayChanged,
    SessionEnding,
}
