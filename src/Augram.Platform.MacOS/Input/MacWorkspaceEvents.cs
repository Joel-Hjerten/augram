using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// The pure half of <see cref="MacSystemEvents"/>, tested on every OS: which notifications it observes, in which center, and
/// what each means as a <see cref="SystemEventKind"/>. The names are AppKit's constants' symbols (their values are the same
/// text; the native half reads each constant's actual value where AppKit exports it) and two undocumented but long-stable
/// distributed notifications for the screen lock.
/// </summary>
internal static class MacWorkspaceEvents
{
    public const string DidActivateApplication = "NSWorkspaceDidActivateApplicationNotification";
    public const string WillSleep = "NSWorkspaceWillSleepNotification";
    public const string DidWake = "NSWorkspaceDidWakeNotification";
    public const string SessionDidResignActive = "NSWorkspaceSessionDidResignActiveNotification";
    public const string SessionDidBecomeActive = "NSWorkspaceSessionDidBecomeActiveNotification";
    public const string WillPowerOff = "NSWorkspaceWillPowerOffNotification";
    public const string ScreenParametersChanged = "NSApplicationDidChangeScreenParametersNotification";
    public const string ScreenIsLocked = "com.apple.screenIsLocked";
    public const string ScreenIsUnlocked = "com.apple.screenIsUnlocked";

    /// <summary>Posted on <c>[[NSWorkspace sharedWorkspace] notificationCenter]</c>, on the main thread.</summary>
    public static IReadOnlyList<string> Workspace { get; } = [DidActivateApplication, WillSleep, DidWake, SessionDidResignActive, SessionDidBecomeActive, WillPowerOff];

    /// <summary>Posted on <c>[NSDistributedNotificationCenter defaultCenter]</c>, delivered on the registering thread's run loop (the main thread's).</summary>
    public static IReadOnlyList<string> Distributed { get; } = [ScreenIsLocked, ScreenIsUnlocked];

    /// <summary>Posted on <c>[NSNotificationCenter defaultCenter]</c> by the application, on the main thread.</summary>
    public static IReadOnlyList<string> Application { get; } = [ScreenParametersChanged];

    /// <summary>
    /// What a notification means: another app became active (the foreground for hold remaps), sleep and wake, the session
    /// switched away and back (fast user switching) or the screen locked and unlocked, displays changed, logging out or
    /// shutting down. Null for anything else.
    /// </summary>
    public static SystemEventKind? Map(string? name) => name switch
    {
        DidActivateApplication => SystemEventKind.ForegroundChanged,
        WillSleep => SystemEventKind.Suspending,
        DidWake => SystemEventKind.Resumed,
        SessionDidResignActive or ScreenIsLocked => SystemEventKind.SessionLocked,
        SessionDidBecomeActive or ScreenIsUnlocked => SystemEventKind.SessionUnlocked,
        WillPowerOff => SystemEventKind.SessionEnding,
        ScreenParametersChanged => SystemEventKind.DisplayChanged,
        _ => null,
    };
}
