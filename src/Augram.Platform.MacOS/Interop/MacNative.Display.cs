using System.Runtime.InteropServices;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// The display-mode part of the macOS P/Invoke surface (learnings 0002 §3): Quartz Display Services for listing a
/// display's modes and applying one in a configuration transaction. <c>CGDisplayCopy…</c> results are owned by the caller
/// (<see cref="CGDisplayModeRelease"/> for a mode, <c>CFRelease</c> for the array); <c>CFArrayGetValueAtIndex</c>
/// borrows. <c>boolean_t</c> results travel as 32-bit integers, <c>bool</c> ones as bytes.
/// </summary>
internal static partial class MacNative
{
    /// <summary><c>kCGConfigurePermanently</c>: what System Settings does (<c>kCGConfigureForAppOnly</c> = 0, <c>ForSession</c> = 1).</summary>
    public const uint CGConfigurePermanently = 2;

    public const int CGErrorSuccess = 0;

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial uint CGMainDisplayID();

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial uint CGDisplayIsBuiltin(uint display);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nint CGDisplayCopyDisplayMode(uint display);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nint CGDisplayCopyAllDisplayModes(uint display, nint options);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial void CGDisplayModeRelease(nint mode);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nuint CGDisplayModeGetWidth(nint mode);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nuint CGDisplayModeGetHeight(nint mode);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nuint CGDisplayModeGetPixelWidth(nint mode);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial nuint CGDisplayModeGetPixelHeight(nint mode);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial double CGDisplayModeGetRefreshRate(nint mode);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial byte CGDisplayModeIsUsableForDesktopGUI(nint mode);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial int CGDisplayModeGetIODisplayModeID(nint mode);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial int CGBeginDisplayConfiguration(out nint config);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial int CGConfigureDisplayWithDisplayMode(nint config, uint display, nint mode, nint options);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial int CGCompleteDisplayConfiguration(nint config, uint option);

    [LibraryImport(CoreGraphicsLibrary)]
    public static partial int CGCancelDisplayConfiguration(nint config);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFDictionaryCreate(nint allocator, ReadOnlySpan<nint> keys, ReadOnlySpan<nint> values, nint count, nint keyCallBacks, nint valueCallBacks);

    /// <summary>The address of an exported variable or struct (<c>kCFTypeDictionaryKeyCallBacks</c> is passed by address; a <c>CFStringRef</c> constant is read from it).</summary>
    public static nint ExportAddress(string library, string symbol) => NativeLibrary.GetExport(NativeLibrary.Load(library), symbol);

    /// <summary>The CoreGraphics framework path, for <see cref="ExportAddress"/>.</summary>
    public static string CoreGraphicsPath => CoreGraphicsLibrary;
}
