using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HoldRemapSpike;

/// <summary>
/// The P/Invoke surface: CoreGraphics event creation and posting, the CoreFoundation run loop, and the few
/// Objective-C runtime calls that read the frontmost app. Constants are Apple's (CGEventTypes.h, Events.h).
/// </summary>
internal static partial class Native
{
    public const string CoreFoundationPath = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    public const string AppKitPath = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string CoreGraphicsPath = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string ObjCPath = "/usr/lib/libobjc.A.dylib";
    private const string SystemPath = "/usr/lib/libSystem.dylib";

    public const uint HidEventTap = 0; // kCGHIDEventTap: as if from the hardware
    public const int CombinedSessionState = 0; // kCGEventSourceStateCombinedSessionState
    public const uint FlagsChanged = 12; // kCGEventFlagsChanged
    public const uint OtherMouseDown = 25;
    public const uint OtherMouseUp = 26;
    public const uint OtherMouseDragged = 27;
    public const uint CenterButton = 2; // kCGMouseButtonCenter
    public const uint ClickStateField = 1; // kCGMouseEventClickState
    public const uint DeltaXField = 4; // kCGMouseEventDeltaX
    public const uint DeltaYField = 5; // kCGMouseEventDeltaY
    public const ulong ShiftFlag = 0x0002_0000; // kCGEventFlagMaskShift
    public const ulong ControlFlag = 0x0004_0000; // kCGEventFlagMaskControl
    public const ulong LeftShiftDeviceFlag = 0x02; // NX_DEVICELSHIFTKEYMASK
    public const ulong LeftControlDeviceFlag = 0x01; // NX_DEVICELCTLKEYMASK
    public const ulong NonCoalescedFlag = 0x100; // NX_NONCOALSESCEDMASK, set on every real event
    public const ushort SpaceKey = 49; // kVK_Space
    public const ushort ShiftKey = 56; // kVK_Shift
    public const ushort ControlKey = 59; // kVK_Control

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint
    {
        public double X;
        public double Y;
    }

    [LibraryImport(CoreGraphicsPath)]
    public static partial nint CGEventCreate(nint source);

    [LibraryImport(CoreGraphicsPath)]
    public static partial CGPoint CGEventGetLocation(nint cgEvent);

    [LibraryImport(CoreGraphicsPath)]
    public static partial nint CGEventCreateKeyboardEvent(nint source, ushort virtualKey, byte keyDown);

    [LibraryImport(CoreGraphicsPath)]
    public static partial nint CGEventCreateMouseEvent(nint source, uint mouseType, CGPoint location, uint mouseButton);

    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGEventSetType(nint cgEvent, uint type);

    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGEventSetFlags(nint cgEvent, ulong flags);

    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGEventSetIntegerValueField(nint cgEvent, uint field, long value);

    [LibraryImport(CoreGraphicsPath)]
    public static partial void CGEventPost(uint tap, nint cgEvent);

    [LibraryImport(CoreGraphicsPath)]
    public static partial ulong CGEventSourceFlagsState(int stateId);

    [LibraryImport(CoreFoundationPath)]
    public static partial void CFRelease(nint cf);

    [LibraryImport(CoreFoundationPath)]
    public static partial int CFRunLoopRunInMode(nint mode, double seconds, byte returnAfterSourceHandled);

    [LibraryImport(ObjCPath, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint objc_getClass(string name);

    [LibraryImport(ObjCPath, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint sel_registerName(string name);

    [LibraryImport(ObjCPath, EntryPoint = "objc_msgSend")]
    public static partial nint SendPtr(nint receiver, nint selector);

    [LibraryImport(ObjCPath)]
    public static partial nint objc_autoreleasePoolPush();

    [LibraryImport(ObjCPath)]
    public static partial void objc_autoreleasePoolPop(nint pool);

    [LibraryImport(SystemPath)]
    public static partial int pthread_main_np();

    /// <summary>Where the pointer is now (a blank event carries the current location).</summary>
    public static CGPoint PointerLocation()
    {
        var blank = CGEventCreate(0);
        try
        {
            return CGEventGetLocation(blank);
        }
        finally
        {
            CFRelease(blank);
        }
    }
}

/// <summary>
/// <c>[[NSWorkspace sharedWorkspace] frontmostApplication].bundleIdentifier</c>. NSWorkspace keeps that value up to
/// date from notifications delivered on the main run loop, which a console app never runs on its own (the value goes
/// stale), so <see cref="MainRunLoop"/> pumps it between reads.
/// </summary>
internal static class Frontmost
{
    private static nint _workspace;
    private static nint _frontmostApplication;
    private static nint _bundleIdentifier;
    private static nint _utf8String;

    public static bool Init()
    {
        NativeLibrary.Load(Native.AppKitPath); // NSWorkspace lives in AppKit, which a console app has not loaded
        var workspaceClass = Native.objc_getClass("NSWorkspace");
        if (workspaceClass == 0)
        {
            return false;
        }

        _workspace = Native.SendPtr(workspaceClass, Native.sel_registerName("sharedWorkspace"));
        _frontmostApplication = Native.sel_registerName("frontmostApplication");
        _bundleIdentifier = Native.sel_registerName("bundleIdentifier");
        _utf8String = Native.sel_registerName("UTF8String");
        return _workspace != 0;
    }

    public static string? BundleId()
    {
        var pool = Native.objc_autoreleasePoolPush();
        try
        {
            var app = Native.SendPtr(_workspace, _frontmostApplication);
            var id = app == 0 ? 0 : Native.SendPtr(app, _bundleIdentifier);
            var utf8 = id == 0 ? 0 : Native.SendPtr(id, _utf8String);
            return utf8 == 0 ? null : Marshal.PtrToStringUTF8(utf8);
        }
        finally
        {
            Native.objc_autoreleasePoolPop(pool);
        }
    }
}

/// <summary>Runs the calling thread's run loop (the main one, from <c>Main</c>) for one slice, then sleeps out whatever is left.</summary>
internal static class MainRunLoop
{
    private static nint _defaultMode;

    public static void Init()
    {
        var coreFoundation = NativeLibrary.Load(Native.CoreFoundationPath);
        _defaultMode = Marshal.ReadIntPtr(NativeLibrary.GetExport(coreFoundation, "kCFRunLoopDefaultMode"));
    }

    public static void Run(TimeSpan slice)
    {
        var start = Stopwatch.GetTimestamp();
        Native.CFRunLoopRunInMode(_defaultMode, slice.TotalSeconds, 0);

        // A run loop with no sources returns at once ("finished"); never spin.
        var left = slice - Stopwatch.GetElapsedTime(start);
        if (left > TimeSpan.FromMilliseconds(1))
        {
            Thread.Sleep(left);
        }
    }
}
