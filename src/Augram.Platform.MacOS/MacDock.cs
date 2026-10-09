using System.Runtime.Versioning;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS;

/// <summary>
/// Augram's Dock icon (Joel, 2026-10-08): shown while the window is open, gone while Augram lives in the menu bar only.
/// The activation policy switches at run time: regular has a Dock icon, an app menu and a Cmd+Tab entry; accessory has
/// none. Main thread only (AppKit); the UI thread is the main thread on macOS.
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacDock
{
    private const nint ActivationPolicyRegular = 0;
    private const nint ActivationPolicyAccessory = 1;

    /// <summary>
    /// Sets the Dock icon from <paramref name="image"/> (any format NSImage reads; Augram passes its .icns). For a process
    /// that is not an app bundle (a development build run as <c>dotnet Augram.App.dll</c>), which macOS otherwise shows
    /// with the generic "exec" icon (Joel, 2026-10-09). A bundle has its icon already; the App calls this only without one.
    /// </summary>
    public static unsafe void SetIcon(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var application = ObjC.Class("NSApplication");
        if (application == 0 || image.Length == 0)
        {
            return;
        }

        nint icon;
        fixed (byte* bytes = image)
        {
            var data = MacNative.SendPtr(ObjC.Class("NSData"), ObjC.Selector("dataWithBytes:length:"), (nint)bytes, (nuint)image.Length);
            icon = MacNative.SendPtr(MacNative.SendPtr(ObjC.Class("NSImage"), ObjC.Selector("alloc")), ObjC.Selector("initWithData:"), data);
        }

        if (icon == 0)
        {
            return;
        }

        var app = MacNative.SendPtr(application, ObjC.Selector("sharedApplication"));
        MacNative.SendVoid(app, ObjC.Selector("setApplicationIconImage:"), icon);
        MacNative.SendVoid(icon, ObjC.Selector("release"));
    }

    /// <summary>True when this process runs from inside an app bundle (<c>…/Augram.app/Contents/MacOS/</c>).</summary>
    public static bool IsBundled(string? processPath) => processPath?.Contains(".app/Contents/MacOS/", StringComparison.Ordinal) == true;

    /// <summary>Shows or hides the Dock icon; showing also brings Augram to the front, as a click on a Dock icon would.</summary>
    public static void SetShown(bool shown)
    {
        var application = ObjC.Class("NSApplication");
        if (application == 0)
        {
            return;
        }

        var app = MacNative.SendPtr(application, ObjC.Selector("sharedApplication"));
        MacNative.SendVoid(app, ObjC.Selector("setActivationPolicy:"), shown ? ActivationPolicyRegular : ActivationPolicyAccessory);
        if (shown)
        {
            MacNative.SendVoid(app, ObjC.Selector("activateIgnoringOtherApps:"), (byte)1);
        }
    }
}
