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

    /// <summary>
    /// SkyLight's private HDR switch, the one behind System Settings › Displays › High Dynamic Range (plan 0014):
    /// <c>bool SLSDisplaySupportsHDRMode(id)</c>, <c>bool SLSDisplayIsHDRModeEnabled(id)</c> and
    /// <c>CGError SLSDisplaySetHDRModeEnabled(id, bool, int, int)</c>, with their <c>CoreDisplay_Display_…</c> twins as
    /// the fallback names. Resolved once at run time, never linked, so a macOS without them leaves
    /// <see cref="IsAvailable"/> false instead of failing to load.
    /// </summary>
    public static unsafe class SkyLightHdr
    {
        private const string SkyLightLibrary = "/System/Library/PrivateFrameworks/SkyLight.framework/SkyLight";

        private static readonly Lazy<Exports?> Resolved = new(Resolve);

        /// <summary>Whether all three calls were found.</summary>
        public static bool IsAvailable => Resolved.Value is not null;

        /// <summary>Whether the display's current mode can show HDR; false when the calls are missing.</summary>
        public static bool Supports(uint display) => Resolved.Value is { } calls && calls.Supports(display) != 0;

        /// <summary>Whether HDR is on; false when the calls are missing.</summary>
        public static bool IsEnabled(uint display) => Resolved.Value is { } calls && calls.IsEnabled(display) != 0;

        /// <summary>Asks for HDR on or off; the <c>CGError</c> (<see cref="CGErrorSuccess"/>), -1 when the calls are missing. The display re-syncs afterwards, so the state changes a moment later.</summary>
        public static int SetEnabled(uint display, bool on)
            => Resolved.Value is { } calls ? calls.SetEnabled(display, on ? (byte)1 : (byte)0, 0, 0) : -1;

        private static Exports? Resolve()
        {
            if (!NativeLibrary.TryLoad(SkyLightLibrary, out var skyLight))
            {
                return null;
            }

            nint Find(string name, string fallback)
                => NativeLibrary.TryGetExport(skyLight, name, out var address) || NativeLibrary.TryGetExport(skyLight, fallback, out address)
                    ? address
                    : 0;

            var supports = Find("SLSDisplaySupportsHDRMode", "CoreDisplay_Display_SupportsHDRMode");
            var isEnabled = Find("SLSDisplayIsHDRModeEnabled", "CoreDisplay_Display_IsHDRModeEnabled");
            var setEnabled = Find("SLSDisplaySetHDRModeEnabled", "CoreDisplay_Display_SetHDRModeEnabled");
            return supports == 0 || isEnabled == 0 || setEnabled == 0
                ? null
                : new Exports(
                    (delegate* unmanaged<uint, byte>)supports,
                    (delegate* unmanaged<uint, byte>)isEnabled,
                    (delegate* unmanaged<uint, byte, int, int, int>)setEnabled);
        }

        private sealed class Exports(
            delegate* unmanaged<uint, byte> supports,
            delegate* unmanaged<uint, byte> isEnabled,
            delegate* unmanaged<uint, byte, int, int, int> setEnabled)
        {
            public byte Supports(uint display) => supports(display);

            public byte IsEnabled(uint display) => isEnabled(display);

            public int SetEnabled(uint display, byte on, int a, int b) => setEnabled(display, on, a, b);
        }
    }
}
