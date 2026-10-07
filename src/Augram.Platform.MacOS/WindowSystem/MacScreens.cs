using System.Runtime.Versioning;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// <c>NSScreen.screens</c> in global top-left points, main screen first. AppKit is the only source of the visible frame
/// (the screen without the menu bar and the Dock); its rectangles are bottom-left based, so each is flipped by the main
/// screen's height. AppKit is main-thread only, so the command executor's calls hop there through <see cref="MainThread"/>
/// (inline when already on it), inside an autorelease pool; a process without a main loop (a test host) reads in place.
/// Empty when AppKit is not loaded.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacScreens
{
    public static IReadOnlyList<MacScreen> All() => MainThread.TryInvoke(ReadAll, out var screens) ? screens : ReadAll();

    /// <summary>The main screen's height in points: the axis every Cocoa ↔ top-left flip turns around. Zero without AppKit.</summary>
    public static double MainHeight() => MainThread.TryInvoke(ReadMainHeight, out var height) ? height : ReadMainHeight();

    private static List<MacScreen> ReadAll()
    {
        var screenClass = ObjC.Class("NSScreen");
        if (screenClass == 0)
        {
            return [];
        }

        using var pool = ObjC.Pool();
        var screens = MacNative.SendPtr(screenClass, ObjC.Selector("screens"));
        var count = screens == 0 ? 0 : (int)MacNative.SendNUInt(screens, ObjC.Selector("count"));
        var result = new List<MacScreen>(count);
        var mainHeight = 0.0;
        for (var i = 0; i < count; i++)
        {
            var screen = MacNative.SendPtr(screens, ObjC.Selector("objectAtIndex:"), (nuint)i);
            var frame = ObjC.Rect(screen, "frame");
            var visible = ObjC.Rect(screen, "visibleFrame");
            if (i == 0)
            {
                mainHeight = frame.Height;
            }

            result.Add(new MacScreen(
                MacRect.FromCocoa(frame.X, frame.Y, frame.Width, frame.Height, mainHeight),
                MacRect.FromCocoa(visible.X, visible.Y, visible.Width, visible.Height, mainHeight)));
        }

        return result;
    }

    private static double ReadMainHeight()
    {
        var screenClass = ObjC.Class("NSScreen");
        if (screenClass == 0)
        {
            return 0;
        }

        using var pool = ObjC.Pool();
        var screens = MacNative.SendPtr(screenClass, ObjC.Selector("screens"));
        if (screens == 0 || MacNative.SendNUInt(screens, ObjC.Selector("count")) == 0)
        {
            return 0;
        }

        return ObjC.Rect(MacNative.SendPtr(screens, ObjC.Selector("objectAtIndex:"), 0), "frame").Height;
    }
}
