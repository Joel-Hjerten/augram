using System.Runtime.Versioning;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// <c>NSScreen.screens</c> in global top-left points, main screen first. AppKit is the only source of the visible frame
/// (the screen without the menu bar and the Dock); its rectangles are bottom-left based, so each is flipped by the main
/// screen's height. Called from the command executor thread inside an autorelease pool; <c>NSScreen</c> is read-only
/// here and AppKit is already loaded by the UI toolkit. Empty when AppKit is not.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacScreens
{
    public static IReadOnlyList<MacScreen> All()
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

    /// <summary>The main screen's height in points: the axis every Cocoa ↔ top-left flip turns around. Zero without AppKit.</summary>
    public static double MainHeight()
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
