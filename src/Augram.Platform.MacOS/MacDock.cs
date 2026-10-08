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
