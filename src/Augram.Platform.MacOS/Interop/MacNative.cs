using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// The macOS P/Invoke surface: CoreFoundation, CoreGraphics, the Accessibility API (HIServices, exported through
/// ApplicationServices), the Objective-C runtime for the few AppKit calls, and libproc. Source-generated with
/// <see cref="LibraryImportAttribute"/> over blittable types; C <c>bool</c>/<c>BOOL</c> travels as a byte. Nothing here
/// has logic: <see cref="Cf"/>, <see cref="Ax"/> and <see cref="ObjC"/> wrap these. Every <c>Copy</c>/<c>Create</c>
/// result is owned by the caller and released with <see cref="CFRelease"/>; <c>Get</c> results are borrowed.
/// </summary>
[SupportedOSPlatform("macos")]
internal static partial class MacNative
{
    public const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreGraphicsLibrary = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string ApplicationServicesLibrary = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";
    private const string SystemLibrary = "/usr/lib/libSystem.dylib";

    public const uint CGWindowListOptionOnScreenOnly = 1 << 0;
    public const uint CGWindowListOptionIncludingWindow = 1 << 3;
    public const uint CFStringEncodingUtf8 = 0x08000100;
    public const int CFNumberSInt64Type = 4;
    public const int CFNumberDoubleType = 13;
    public const int AXValueCGPointType = 1;
    public const int AXValueCGSizeType = 2;
    public const int ProcPidPathInfoMaxSize = 4096;

    public const int AXErrorSuccess = 0;
    public const int AXErrorInvalidUIElement = -25202;
    public const int AXErrorCannotComplete = -25204;
    public const int AXErrorAttributeUnsupported = -25205;
    public const int AXErrorActionUnsupported = -25206;
    public const int AXErrorApiDisabled = -25211;
    public const int AXErrorNoValue = -25212;

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    // CoreFoundation

    [LibraryImport(CoreFoundationLibrary)]
    public static partial void CFRelease(nint cf);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFRetain(nint cf);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nuint CFGetTypeID(nint cf);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nuint CFStringGetTypeID();

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nuint CFBooleanGetTypeID();

    [LibraryImport(CoreFoundationLibrary)]
    public static partial byte CFBooleanGetValue(nint boolean);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFArrayGetCount(nint array);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFArrayGetValueAtIndex(nint array, nint index);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFDictionaryGetValue(nint dictionary, nint key);

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFNumberGetValue")]
    public static partial byte CFNumberGetInt64(nint number, int type, out long value);

    [LibraryImport(CoreFoundationLibrary, EntryPoint = "CFNumberGetValue")]
    public static partial byte CFNumberGetDouble(nint number, int type, out double value);

    [LibraryImport(CoreFoundationLibrary, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint CFStringCreateWithCString(nint allocator, string text, uint encoding);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFStringGetLength(nint text);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFStringGetMaximumSizeForEncoding(nint length, uint encoding);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial byte CFStringGetCString(nint text, Span<byte> buffer, nint bufferSize, uint encoding);

    // CoreGraphics

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nint CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial byte CGRectMakeWithDictionaryRepresentation(nint dictionary, out CGRect rect);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial int CGGetActiveDisplayList(uint maxDisplays, Span<uint> displays, out uint displayCount);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial CGRect CGDisplayBounds(uint display);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nint CGEventCreate(nint source);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial CGPoint CGEventGetLocation(nint cgEvent);

    // Accessibility (HIServices)

    [LibraryImport(ApplicationServicesLibrary)]
    public static partial byte AXIsProcessTrusted();

    [LibraryImport(ApplicationServicesLibrary)]
    public static partial nint AXUIElementCreateApplication(int pid);

    [LibraryImport(ApplicationServicesLibrary)]
    public static partial nint AXUIElementCreateSystemWide();

    [LibraryImport(ApplicationServicesLibrary)]
    public static partial int AXUIElementCopyAttributeValue(nint element, nint attribute, out nint value);

    [LibraryImport(ApplicationServicesLibrary)]
    public static partial int AXUIElementSetAttributeValue(nint element, nint attribute, nint value);

    [LibraryImport(ApplicationServicesLibrary)]
    public static partial int AXUIElementPerformAction(nint element, nint action);

    [LibraryImport(ApplicationServicesLibrary)]
    public static partial int AXUIElementSetMessagingTimeout(nint element, float timeoutInSeconds);

    [LibraryImport(ApplicationServicesLibrary)]
    public static partial int AXUIElementGetPid(nint element, out int pid);

    [LibraryImport(ApplicationServicesLibrary, EntryPoint = "AXValueCreate")]
    public static partial nint AXValueCreatePoint(int type, in CGPoint value);

    [LibraryImport(ApplicationServicesLibrary, EntryPoint = "AXValueCreate")]
    public static partial nint AXValueCreateSize(int type, in CGSize value);

    [LibraryImport(ApplicationServicesLibrary, EntryPoint = "AXValueGetValue")]
    public static partial byte AXValueGetPoint(nint value, int type, out CGPoint point);

    [LibraryImport(ApplicationServicesLibrary, EntryPoint = "AXValueGetValue")]
    public static partial byte AXValueGetSize(nint value, int type, out CGSize size);

    /// <summary>
    /// The <c>CGWindowID</c> behind an AX window element. Private API, but present since Mac OS X 10.5 and what every
    /// macOS window manager (Rectangle, Amethyst, yabai, Hammerspoon) uses to tie the two worlds together.
    /// </summary>
    [LibraryImport(ApplicationServicesLibrary, EntryPoint = "_AXUIElementGetWindow")]
    public static partial int AXUIElementGetWindow(nint element, out uint windowId);

    // Objective-C runtime (AppKit classes are already loaded by the UI toolkit)

    [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint objc_getClass(string name);

    [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint sel_registerName(string name);

    [LibraryImport(ObjCLibrary)]
    public static partial nint objc_autoreleasePoolPush();

    [LibraryImport(ObjCLibrary)]
    public static partial void objc_autoreleasePoolPop(nint pool);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial nint SendPtr(nint receiver, nint selector);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial nint SendPtr(nint receiver, nint selector, nuint argument);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial nint SendNInt(nint receiver, nint selector);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial nuint SendNUInt(nint receiver, nint selector);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial byte SendBool(nint receiver, nint selector);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial byte SendBool(nint receiver, nint selector, nint argument);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(nint receiver, nint selector, byte argument);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(nint receiver, nint selector, nint argument);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(nint receiver, nint selector, nuint argument);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(nint receiver, nint selector, CGRect rect, byte flag);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial nint SendPtr(nint receiver, nint selector, nint argument);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial nint SendPtr(nint receiver, nint selector, double argument);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial nint SendPtr(nint receiver, nint selector, nint first, nuint second);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial nint SendPtr(nint receiver, nint selector, nint first, nint second, nint third);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(nint receiver, nint selector);

    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial void SendVoid(nint receiver, nint selector, CGSize size);

    [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint objc_allocateClassPair(nint superclass, string name, nuint extraBytes);

    [LibraryImport(ObjCLibrary)]
    public static partial void objc_registerClassPair(nint cls);

    [LibraryImport(ObjCLibrary, StringMarshalling = StringMarshalling.Utf8)]
    public static unsafe partial byte class_addMethod(nint cls, nint selector, delegate* unmanaged<nint, nint, nint, void> implementation, string types);

    /// <summary>A method returning <c>NSRect</c> on arm64, where a four-double struct comes back in registers.</summary>
    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
    public static partial CGRect SendRect(nint receiver, nint selector);

    /// <summary>The same on x86-64, where a 32-byte struct is returned through memory.</summary>
    [LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend_stret")]
    public static partial void SendRectStret(out CGRect result, nint receiver, nint selector);

    // libproc

    [LibraryImport(SystemLibrary)]
    public static partial int proc_pidpath(int pid, Span<byte> buffer, uint bufferSize);

    // libdispatch and pthread

    /// <summary>The main dispatch queue is the global object itself (<c>dispatch_get_main_queue()</c> is <c>&amp;_dispatch_main_q</c>).</summary>
    public static nint MainQueue() => NativeLibrary.GetExport(NativeLibrary.Load(SystemLibrary), "_dispatch_main_q");

    [LibraryImport(SystemLibrary)]
    public static unsafe partial void dispatch_async_f(nint queue, nint context, delegate* unmanaged<nint, void> work);

    [LibraryImport(SystemLibrary)]
    public static partial int pthread_main_np();
}
