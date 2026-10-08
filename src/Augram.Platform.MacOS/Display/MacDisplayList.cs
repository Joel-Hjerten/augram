using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Augram.Platform.MacOS.Interop;
using Augram.Platform.MacOS.WindowSystem;

namespace Augram.Platform.MacOS.Display;

/// <summary>
/// Reads the active displays and their modes through Quartz Display Services and applies one mode in a configuration
/// transaction (learnings 0002 §3). Modes are listed with <c>kCGDisplayShowDuplicateLowResolutionModes</c>, so the 1x
/// twins of HiDPI sizes and their TV rates are there too ("Show all resolutions"). A mode is applied by finding the same
/// <c>CGDisplayMode</c> again (IOKit mode id, sizes and rate) and configuring it <c>kCGConfigurePermanently</c>, as
/// System Settings does; a failed step cancels the transaction. CoreGraphics only, no AppKit, so it runs on the calling
/// thread (the command executor, or the UI thread for a step form).
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacDisplayList
{
    private const int MaxDisplays = 16;

    private static readonly Lazy<nint> AllModesOptions = new(CreateAllModesOptions);

    public static IReadOnlyList<MacDisplayReading> Read()
    {
        Span<uint> ids = stackalloc uint[MaxDisplays];
        if (MacNative.CGGetActiveDisplayList(MaxDisplays, ids, out var count) != 0)
        {
            return [];
        }

        var main = MacNative.CGMainDisplayID();
        var displays = new List<MacDisplayReading>((int)count);
        foreach (var id in ids[..(int)Math.Min(count, MaxDisplays)])
        {
            var bounds = MacNative.CGDisplayBounds(id);
            displays.Add(new MacDisplayReading(
                id,
                new MacRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                id == main,
                MacNative.CGDisplayIsBuiltin(id) != 0,
                CurrentMode(id),
                AllModes(id)));
        }

        return displays;
    }

    /// <summary>Applies the display's mode that matches <paramref name="target"/>; the CoreGraphics error, or a reason when the mode is gone.</summary>
    public static string? Apply(uint display, MacNativeMode target)
    {
        var modes = MacNative.CGDisplayCopyAllDisplayModes(display, AllModesOptions.Value);
        if (modes == 0)
        {
            return "the display's modes could not be read";
        }

        try
        {
            var count = MacNative.CFArrayGetCount(modes);
            for (nint i = 0; i < count; i++)
            {
                var mode = MacNative.CFArrayGetValueAtIndex(modes, i);
                if (Describe(mode) == target)
                {
                    return Configure(display, mode);
                }
            }

            return "the display no longer offers that mode";
        }
        finally
        {
            MacNative.CFRelease(modes);
        }
    }

    private static string? Configure(uint display, nint mode)
    {
        var error = MacNative.CGBeginDisplayConfiguration(out var config);
        if (error != MacNative.CGErrorSuccess)
        {
            return $"CGBeginDisplayConfiguration failed ({error})";
        }

        error = MacNative.CGConfigureDisplayWithDisplayMode(config, display, mode, 0);
        if (error != MacNative.CGErrorSuccess)
        {
            MacNative.CGCancelDisplayConfiguration(config);
            return $"CGConfigureDisplayWithDisplayMode failed ({error})";
        }

        error = MacNative.CGCompleteDisplayConfiguration(config, MacNative.CGConfigurePermanently);
        return error == MacNative.CGErrorSuccess ? null : $"CGCompleteDisplayConfiguration failed ({error})";
    }

    private static MacNativeMode? CurrentMode(uint display)
    {
        var mode = MacNative.CGDisplayCopyDisplayMode(display);
        if (mode == 0)
        {
            return null;
        }

        try
        {
            return Describe(mode);
        }
        finally
        {
            MacNative.CGDisplayModeRelease(mode);
        }
    }

    private static List<MacNativeMode> AllModes(uint display)
    {
        var modes = MacNative.CGDisplayCopyAllDisplayModes(display, AllModesOptions.Value);
        if (modes == 0)
        {
            return [];
        }

        try
        {
            var count = MacNative.CFArrayGetCount(modes);
            var result = new List<MacNativeMode>((int)count);
            for (nint i = 0; i < count; i++)
            {
                result.Add(Describe(MacNative.CFArrayGetValueAtIndex(modes, i)));
            }

            return result;
        }
        finally
        {
            MacNative.CFRelease(modes);
        }
    }

    private static MacNativeMode Describe(nint mode) => new(
        (int)MacNative.CGDisplayModeGetWidth(mode),
        (int)MacNative.CGDisplayModeGetHeight(mode),
        (int)MacNative.CGDisplayModeGetPixelWidth(mode),
        (int)MacNative.CGDisplayModeGetPixelHeight(mode),
        MacNative.CGDisplayModeGetRefreshRate(mode),
        MacNative.CGDisplayModeIsUsableForDesktopGUI(mode) != 0,
        MacNative.CGDisplayModeGetIODisplayModeID(mode));

    /// <summary><c>{ kCGDisplayShowDuplicateLowResolutionModes: true }</c>, created once and kept for the process; 0 when the key cannot be found.</summary>
    private static nint CreateAllModesOptions()
    {
        var key = Marshal.ReadIntPtr(MacNative.ExportAddress(MacNative.CoreGraphicsPath, "kCGDisplayShowDuplicateLowResolutionModes"));
        if (key == 0)
        {
            return 0;
        }

        ReadOnlySpan<nint> keys = [key];
        ReadOnlySpan<nint> values = [Cf.True];
        return MacNative.CFDictionaryCreate(
            0,
            keys,
            values,
            1,
            MacNative.ExportAddress(MacNative.CoreFoundationLibrary, "kCFTypeDictionaryKeyCallBacks"),
            MacNative.ExportAddress(MacNative.CoreFoundationLibrary, "kCFTypeDictionaryValueCallBacks"));
    }
}
