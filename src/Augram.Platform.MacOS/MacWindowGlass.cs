using System.Runtime.Versioning;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS;

/// <summary>
/// The glass behind an Augram window (Joel, 2026-10-12). Avalonia 11.3's macOS backend blurs a transparent window with an
/// <c>NSVisualEffectView</c> it makes a direct subview of the window's content view (<c>AutoFitContentView</c>'s
/// <c>_blurBehind</c>, blending behind the window), with the deprecated always-light material that follows the window's
/// active state: unfocused, the glass became a flat light fill, white under a 0 % tint. This gives that view a material
/// that follows the window's appearance (Avalonia sets the window's <c>NSAppearance</c> from the theme) and the state the
/// background asks for. Depends on Avalonia's view layout: re-check on an Avalonia upgrade. Main thread only (AppKit);
/// the UI thread is the main thread on macOS.
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacWindowGlass
{
    /// <summary><c>NSVisualEffectMaterialUnderWindowBackground</c>: the window background's material, light or dark with the appearance.</summary>
    private const nint Material = 21;

    private const nint BlendingModeBehindWindow = 0;
    private const nint StateFollowsWindowActiveState = 0;
    private const nint StateActive = 1;

    /// <summary>
    /// Sets the material and state of the blur view behind <paramref name="nsWindow"/> (an <c>NSWindow*</c>): with
    /// <paramref name="keepWhenInactive"/> the glass stays while the window is unfocused, else it follows the window's
    /// active state (unfocused, the material's opaque fill). Idempotent. False when the window has no such view.
    /// </summary>
    public static bool TrySet(nint nsWindow, bool keepWhenInactive)
    {
        var effectViewClass = ObjC.Class("NSVisualEffectView");
        if (nsWindow == 0 || effectViewClass == 0)
        {
            return false;
        }

        using var pool = ObjC.Pool();
        var contentView = MacNative.SendPtr(nsWindow, ObjC.Selector("contentView"));
        if (contentView == 0)
        {
            return false;
        }

        var subviews = MacNative.SendPtr(contentView, ObjC.Selector("subviews"));
        var count = subviews == 0 ? 0 : MacNative.SendNUInt(subviews, ObjC.Selector("count"));
        for (nuint i = 0; i < count; i++)
        {
            var view = MacNative.SendPtr(subviews, ObjC.Selector("objectAtIndex:"), i);
            if (MacNative.SendBool(view, ObjC.Selector("isKindOfClass:"), effectViewClass) != 0
                && MacNative.SendNInt(view, ObjC.Selector("blendingMode")) == BlendingModeBehindWindow)
            {
                MacNative.SendVoid(view, ObjC.Selector("setMaterial:"), Material);
                MacNative.SendVoid(view, ObjC.Selector("setState:"), keepWhenInactive ? StateActive : StateFollowsWindowActiveState);
                return true;
            }
        }

        return false;
    }
}
