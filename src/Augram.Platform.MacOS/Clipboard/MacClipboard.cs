using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Clipboard;

/// <summary>
/// The macOS <see cref="IClipboard"/> (the Clear clipboard step): <c>[[NSPasteboard generalPasteboard] clearContents]</c>.
/// The pasteboard is a server-side object, safe to call from the command executor thread without the main thread; an
/// autorelease pool catches what AppKit autoreleases there. Without AppKit loaded (no <c>NSPasteboard</c> class) it
/// reports not supported and the step skips. Not unit-tested: a test would clear the real pasteboard.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacClipboard : IClipboard
{
    public ClipboardResult Clear()
    {
        using var pool = ObjC.Pool();
        var pasteboardClass = ObjC.Class("NSPasteboard");
        if (pasteboardClass == 0)
        {
            return ClipboardResult.NotSupported("AppKit is not loaded, so there is no pasteboard");
        }

        var pasteboard = MacNative.SendPtr(pasteboardClass, ObjC.Selector("generalPasteboard"));
        if (pasteboard == 0)
        {
            return ClipboardResult.Failed("the general pasteboard is not available");
        }

        // Returns the new change count; clearing cannot fail once the pasteboard is there.
        MacNative.SendNInt(pasteboard, ObjC.Selector("clearContents"));
        return ClipboardResult.Ok;
    }
}
