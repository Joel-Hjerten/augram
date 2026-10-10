using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Input;

/// <summary>
/// The <see cref="ISystemEvents"/> on macOS (plan 0002 step 3; <b>compiled, not yet run on a Mac</b>): another app becoming
/// active (<see cref="SystemEventKind.ForegroundChanged"/>, so a hold key pressed right after switching into Blender already
/// belongs to it, decision 5), sleep and wake, the session switching away and back and the screen locking and unlocking,
/// displays changing, and power-off, as <see cref="MacWorkspaceEvents"/> maps them. Selector-based observers on a small
/// runtime-registered <c>NSObject</c> subclass (<c>AugramSystemEventsObserver</c>, its one method an
/// <c>UnmanagedCallersOnly</c> callback, as <see cref="MacStatusItem"/>'s target is), in NSWorkspace's notification center,
/// the distributed center and the default center. Registered on the main thread (the distributed center delivers on the
/// registering thread's run loop; the other two post on the main thread): inline when constructed there, as the App's
/// composition root does; dispatched otherwise, and a process with no main loop (a test host) registers nothing.
/// <see cref="Occurred"/> fires on the main thread; listeners must return at once. Without AppKit nothing is observed.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed unsafe class MacSystemEvents : ISystemEvents, IDisposable
{
    private const string ObserverClassName = "AugramSystemEventsObserver";

    private static readonly ConcurrentDictionary<nint, MacSystemEvents> ByObserver = new();
    private static readonly Lazy<nint> ObserverClass = new(RegisterObserverClass);

    private readonly Dictionary<string, SystemEventKind> _byName = new(StringComparer.Ordinal);
    private readonly List<nint> _centers = [];
    private nint _observer;
    private int _disposed;

    public MacSystemEvents()
    {
        MainThread.TryInvoke(Register, out _);
    }

    public event EventHandler<SystemEventKind>? Occurred;

    /// <summary>True once the observers are registered (AppKit loaded, the main thread reached).</summary>
    public bool IsObserving => Volatile.Read(ref _observer) != 0;

    /// <summary>Removes the observer from every center (they are thread-safe) and releases it.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var observer = Interlocked.Exchange(ref _observer, 0);
        if (observer == 0)
        {
            return;
        }

        ByObserver.TryRemove(observer, out _);
        foreach (var center in _centers)
        {
            MacNative.SendVoid(center, ObjC.Selector("removeObserver:"), observer);
        }

        MacNative.SendVoid(observer, ObjC.Selector("release"));
    }

    /// <summary>Main thread: the observer object and its registrations; false when AppKit is not loaded.</summary>
    private bool Register()
    {
        var workspaceClass = ObjC.Class("NSWorkspace");
        if (workspaceClass == 0 || ObserverClass.Value == 0)
        {
            return false;
        }

        var observer = MacNative.SendPtr(MacNative.SendPtr(ObserverClass.Value, ObjC.Selector("alloc")), ObjC.Selector("init"));
        ByObserver[observer] = this;
        var workspace = MacNative.SendPtr(workspaceClass, ObjC.Selector("sharedWorkspace"));
        Observe(observer, MacNative.SendPtr(workspace, ObjC.Selector("notificationCenter")), MacWorkspaceEvents.Workspace);
        Observe(observer, MacNative.SendPtr(ObjC.Class("NSDistributedNotificationCenter"), ObjC.Selector("defaultCenter")), MacWorkspaceEvents.Distributed);
        Observe(observer, MacNative.SendPtr(ObjC.Class("NSNotificationCenter"), ObjC.Selector("defaultCenter")), MacWorkspaceEvents.Application);
        Volatile.Write(ref _observer, observer);
        return true;
    }

    /// <summary>One registration per name; the kind is keyed by the name's actual text, so the callback maps whatever value AppKit's constant has.</summary>
    private void Observe(nint observer, nint center, IReadOnlyList<string> symbols)
    {
        if (center == 0)
        {
            return;
        }

        _centers.Add(center);
        foreach (var symbol in symbols)
        {
            var name = ObjC.AppKitString(symbol);
            _byName[Cf.ReadString(name) ?? symbol] = MacWorkspaceEvents.Map(symbol)!.Value;
            MacNative.SendVoid(center, ObjC.Selector("addObserver:selector:name:object:"), observer, ObjC.Selector("augramNotified:"), name, 0);
        }
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

        MacNative.class_addMethod(cls, ObjC.Selector("augramNotified:"), &OnNotification, "v@:@");
        MacNative.objc_registerClassPair(cls);
        return cls;
    }

    [UnmanagedCallersOnly]
    private static void OnNotification(nint self, nint selector, nint notification)
    {
        if (!ByObserver.TryGetValue(self, out var events))
        {
            return;
        }

        try
        {
            var name = Cf.ReadString(MacNative.SendPtr(notification, ObjC.Selector("name")));
            if (name is not null && events._byName.TryGetValue(name, out var kind))
            {
                events.Occurred?.Invoke(events, kind);
            }
        }
        catch (Exception)
        {
            // An exception must not unwind into AppKit; the event is lost, the backstops (the focus poll, the hook watchdog) remain.
        }
    }
}
