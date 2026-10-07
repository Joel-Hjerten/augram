using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;
using Augram.Platform.MacOS.WindowSystem;

namespace Augram.Platform.MacOS.Overlay;

/// <summary>
/// The macOS <see cref="IOverlayWindowStyle"/> for the trail window (F6, CLAUDE.md invariant 6). Click-through is
/// <c>ignoresMouseEvents = YES</c>, read back after it is set; the window sits at the status-window level so the trail is
/// drawn over the Dock and the menu bar, joins every Space and full-screen apps, and stays out of the Cmd-` cycle.
/// <see cref="Place"/> takes global top-left points (macOS reports the pointer, Avalonia's screens and window positions
/// in points, never pixels) and sets the frame directly, which also lets the borderless window cover the menu bar.
/// Every call comes from the UI thread, as AppKit requires.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOverlayWindowStyle : IOverlayWindowStyle
{
    // NSStatusWindowLevel: above the Dock (20) and the main menu (24).
    private const nint StatusWindowLevel = 25;

    // NSWindowCollectionBehavior: CanJoinAllSpaces | Stationary | IgnoresCycle | FullScreenAuxiliary.
    private const nuint IgnoresCycle = 1 << 6;
    private const nuint Behavior = (1 << 0) | (1 << 4) | IgnoresCycle | (1 << 8);

    public OverlayStyleReport Apply(nint handle)
    {
        var window = WindowOf(handle);
        if (window == 0)
        {
            return new OverlayStyleReport(false, false, false, "no NSWindow");
        }

        MacNative.SendVoid(window, ObjC.Selector("setIgnoresMouseEvents:"), (byte)1);
        MacNative.SendVoid(window, ObjC.Selector("setLevel:"), StatusWindowLevel);
        MacNative.SendVoid(window, ObjC.Selector("setCollectionBehavior:"), Behavior);
        MacNative.SendVoid(window, ObjC.Selector("setHasShadow:"), (byte)0);

        var clickThrough = MacNative.SendBool(window, ObjC.Selector("ignoresMouseEvents")) != 0;
        var level = MacNative.SendNInt(window, ObjC.Selector("level"));
        var behavior = MacNative.SendNUInt(window, ObjC.Selector("collectionBehavior"));
        var isKey = MacNative.SendBool(window, ObjC.Selector("isKeyWindow")) != 0;
        return new OverlayStyleReport(
            clickThrough,
            NoActivate: !isKey,
            ToolWindow: (behavior & IgnoresCycle) != 0,
            $"ignoresMouseEvents={(clickThrough ? 1 : 0)} level={level} behavior=0x{behavior:X}");
    }

    public void Place(nint handle, int x, int y, int width, int height)
    {
        var window = WindowOf(handle);
        if (window == 0)
        {
            return;
        }

        // Click-through is re-asserted on every placement: it costs nothing and is the one property that must never lapse.
        MacNative.SendVoid(window, ObjC.Selector("setIgnoresMouseEvents:"), (byte)1);
        var frame = new MacRect(x, y, width, height);
        var cocoa = new MacNative.CGRect { X = x, Y = frame.CocoaY(MacScreens.MainHeight()), Width = width, Height = height };
        MacNative.SendVoid(window, ObjC.Selector("setFrame:display:"), cocoa, (byte)1);
    }

    /// <summary>The <c>NSWindow</c> behind the toolkit's handle, which is the window itself or its content view depending on the version.</summary>
    private static nint WindowOf(nint handle)
    {
        if (handle == 0)
        {
            return 0;
        }

        if (ObjC.RespondsTo(handle, "setIgnoresMouseEvents:"))
        {
            return handle;
        }

        return ObjC.RespondsTo(handle, "window") ? MacNative.SendPtr(handle, ObjC.Selector("window")) : 0;
    }
}
