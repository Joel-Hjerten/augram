using System.Globalization;
using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.Windows.Interop;

namespace Augram.Platform.Windows.Overlay;

/// <summary>
/// The <see cref="IOverlayWindowStyle"/> for Win32. ORs into the extended style: <c>WS_EX_LAYERED</c> plus
/// <c>WS_EX_TRANSPARENT</c> (the documented pair for a top-level window that passes mouse input through;
/// alpha is set to opaque so painting is unaffected), <c>WS_EX_NOACTIVATE</c> (never takes focus) and
/// <c>WS_EX_TOOLWINDOW</c> (no taskbar button, no Alt-Tab); then re-asserts topmost without activating
/// and reads the style back. Avalonia rewrites <c>GWL_EXSTYLE</c> on every <c>Show()</c> and drops these
/// (learnings 0001 B2), so the App calls this after showing and verifies the report before the window
/// is allowed to cover the screen.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OverlayWindowStyle : IOverlayWindowStyle
{
    private const long ClickThrough = NativeMethods.WsExLayered | NativeMethods.WsExTransparent;
    private const long Wanted = ClickThrough | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;

    public OverlayStyleReport Apply(nint handle)
    {
        if (handle == 0)
        {
            return new OverlayStyleReport(false, false, false, "no handle");
        }

        var current = (long)NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle);
        var wanted = current | Wanted;
        if (wanted != current)
        {
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, (nint)wanted);
        }

        NativeMethods.SetLayeredWindowAttributes(handle, 0, byte.MaxValue, NativeMethods.LwaAlpha);
        NativeMethods.SetWindowPos(handle, NativeMethods.HwndTopmost, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
        return Read(handle);
    }

    public void Place(nint handle, int x, int y, int width, int height)
    {
        if (handle != 0)
        {
            NativeMethods.SetWindowPos(handle, NativeMethods.HwndTopmost, x, y, width, height, NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
        }
    }

    /// <summary>The current styles without changing them.</summary>
    public static OverlayStyleReport Read(nint handle)
    {
        var style = (long)NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle);
        return new OverlayStyleReport(
            (style & ClickThrough) == ClickThrough,
            (style & NativeMethods.WsExNoActivate) != 0,
            (style & NativeMethods.WsExToolWindow) != 0,
            "0x" + style.ToString("X", CultureInfo.InvariantCulture));
    }
}
