using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Startup;

/// <summary>
/// Whether macOS started this process as a login item (F7, 2026-10-10; <b>compiled, not yet run on a Mac</b>). A login item
/// registered through <c>SMAppService</c> takes no arguments, so Augram cannot ask for a hidden launch as the Windows Run
/// entry does (<c>--hidden</c>); it detects one, as Electron's <c>wasOpenedAtLogin</c> does
/// (<c>electron_application_delegate.mm</c>): while <c>NSApplicationDidFinishLaunchingNotification</c> is delivered, the
/// current Apple event is the launch's own, and a login launch is an open-application event whose launch property is
/// <c>keyAELaunchedAsLogInItem</c> (<see cref="MacLoginItems.IsLoginItemLaunch"/>). The notification comes from inside
/// <c>[NSApp run]</c>, which Avalonia starts in its main loop; <see cref="Observe"/> must run before that, on the main
/// thread (the App calls it in <c>OnFrameworkInitializationCompleted</c>). Called later, once the application runs, it reads
/// the current event at once (by then no launch event: an ordinary launch). Without AppKit it answers an ordinary launch.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed unsafe class MacLaunchEvent : IDisposable
{
    private const string ObserverClassName = "AugramLaunchObserver";
    private const string DidFinishLaunching = "NSApplicationDidFinishLaunchingNotification";

    private static readonly ConcurrentDictionary<nint, MacLaunchEvent> ByObserver = new();
    private static readonly Lazy<nint> ObserverClass = new(RegisterObserverClass);

    private readonly Lock _gate = new();
    private MacLaunch? _launch;
    private Action<MacLaunch>? _then;
    private nint _observer;

    private MacLaunchEvent()
    {
    }

    /// <summary>Main thread, before Avalonia's main loop: starts listening for the end of launching.</summary>
    public static MacLaunchEvent Observe()
    {
        var launchEvent = new MacLaunchEvent();
        launchEvent.Start();
        return launchEvent;
    }

    /// <summary>
    /// Runs <paramref name="then"/> once with the launch: at once when it is known, else on the main thread inside the
    /// notification's delivery (so it must return at once; the App posts to its dispatcher). A second call replaces the first.
    /// </summary>
    public void WhenKnown(Action<MacLaunch> then)
    {
        ArgumentNullException.ThrowIfNull(then);
        MacLaunch? known;
        lock (_gate)
        {
            known = _launch;
            _then = known is null ? then : null;
        }

        if (known is not null)
        {
            then(known);
        }
    }

    /// <summary>Removes the observer from the notification center and releases it (the notification comes once per process, so it is simply left registered until then).</summary>
    public void Dispose()
    {
        var observer = Interlocked.Exchange(ref _observer, 0);
        if (observer == 0)
        {
            return;
        }

        ByObserver.TryRemove(observer, out _);
        MacNative.SendVoid(MacNative.SendPtr(ObjC.Class("NSNotificationCenter"), ObjC.Selector("defaultCenter")), ObjC.Selector("removeObserver:"), observer);
        MacNative.SendVoid(observer, ObjC.Selector("release"));
    }

    private void Start()
    {
        var application = ObjC.Class("NSApplication");
        if (application == 0 || ObserverClass.Value == 0)
        {
            Settle(new MacLaunch(false, "no AppKit"));
            return;
        }

        if (MacNative.SendBool(MacNative.SendPtr(application, ObjC.Selector("sharedApplication")), ObjC.Selector("isRunning")) != 0)
        {
            // Too late to see the notification: the application already runs (not the App's order).
            Settle(ReadCurrentEvent());
            return;
        }

        var observer = MacNative.SendPtr(MacNative.SendPtr(ObserverClass.Value, ObjC.Selector("alloc")), ObjC.Selector("init"));
        ByObserver[observer] = this;
        _observer = observer;
        var center = MacNative.SendPtr(ObjC.Class("NSNotificationCenter"), ObjC.Selector("defaultCenter"));
        MacNative.SendVoid(center, ObjC.Selector("addObserver:selector:name:object:"), observer, ObjC.Selector("augramLaunched:"), ObjC.AppKitString(DidFinishLaunching), 0);
    }

    private void Settle(MacLaunch launch)
    {
        Action<MacLaunch>? then;
        lock (_gate)
        {
            if (_launch is not null)
            {
                return;
            }

            _launch = launch;
            then = _then;
            _then = null;
        }

        then?.Invoke(launch);
    }

    /// <summary>The Apple event being handled now: during the notification, the launch's open-application event.</summary>
    private static MacLaunch ReadCurrentEvent()
    {
        using var pool = ObjC.Pool();
        var managerClass = ObjC.Class("NSAppleEventManager");
        var manager = managerClass == 0 ? 0 : MacNative.SendPtr(managerClass, ObjC.Selector("sharedAppleEventManager"));
        var current = manager == 0 ? 0 : MacNative.SendPtr(manager, ObjC.Selector("currentAppleEvent"));
        if (current == 0)
        {
            return new MacLaunch(false, "none");
        }

        var eventClass = MacNative.SendUInt32(current, ObjC.Selector("eventClass"));
        var eventId = MacNative.SendUInt32(current, ObjC.Selector("eventID"));
        var property = MacNative.SendPtrUInt32(current, ObjC.Selector("paramDescriptorForKeyword:"), MacLoginItems.PropData);
        var launchProperty = property == 0 ? 0u : MacNative.SendUInt32(property, ObjC.Selector("enumCodeValue"));
        return new MacLaunch(MacLoginItems.IsLoginItemLaunch(eventClass, eventId, launchProperty), MacLoginItems.Describe(eventClass, eventId, launchProperty));
    }

    private static nint RegisterObserverClass()
    {
        var superclass = ObjC.Class("NSObject");
        if (superclass == 0)
        {
            return 0;
        }

        var cls = MacNative.objc_allocateClassPair(superclass, ObserverClassName, 0);
        if (cls == 0)
        {
            // Registered already (by an earlier load in this process): use it.
            return ObjC.Class(ObserverClassName);
        }

        MacNative.class_addMethod(cls, ObjC.Selector("augramLaunched:"), &OnFinishedLaunching, "v@:@");
        MacNative.objc_registerClassPair(cls);
        return cls;
    }

    [UnmanagedCallersOnly]
    private static void OnFinishedLaunching(nint self, nint selector, nint notification)
    {
        if (!ByObserver.TryGetValue(self, out var launchEvent))
        {
            return;
        }

        try
        {
            // Only the first answer counts (Settle); the observer is not released here, inside its own method.
            launchEvent.Settle(ReadCurrentEvent());
        }
        catch (Exception)
        {
            // An exception must not unwind into AppKit. The App's fallback shows the window when no answer comes.
        }
    }
}
