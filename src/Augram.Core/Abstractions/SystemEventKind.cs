namespace Augram.Core.Abstractions;

/// <summary>OS events that can invalidate a global hook or the capture state (N4: session switch, power, display), and focus moving to another app.</summary>
public enum SystemEventKind
{
    SessionLocked,
    SessionUnlocked,
    Suspending,
    Resumed,
    DisplayChanged,
    SessionEnding,

    /// <summary>
    /// Another window or app came to the front (F9 "instant", plan 0002 decision 5): the engine's watch checks focus at once
    /// instead of at its next poll, so a hold key pressed right after switching into an app already belongs to it. Not a
    /// session event: the hook health monitor neither logs it nor restarts its silence clock for it.
    /// </summary>
    ForegroundChanged,
}
