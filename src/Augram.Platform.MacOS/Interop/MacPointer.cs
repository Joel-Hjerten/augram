using System.Runtime.Versioning;

namespace Augram.Platform.MacOS.Interop;

/// <summary>
/// Where the pointer is now, the one way it is read: a blank <c>CGEvent</c> carries the current location, in global top-left
/// points like the hook. CoreGraphics only, any thread. <see cref="Input.MacCursorProbe"/> (the hook watchdog) and the remap
/// button's release (<see cref="Input.MacRemapButtonSimulator"/>) read it here.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacPointer
{
    /// <summary>The pointer's location; false (and the origin) when CoreGraphics cannot make an event.</summary>
    public static bool TryRead(out MacNative.CGPoint at)
    {
        var blank = MacNative.CGEventCreate(0);
        if (blank == 0)
        {
            at = default;
            return false;
        }

        try
        {
            at = MacNative.CGEventGetLocation(blank);
            return true;
        }
        finally
        {
            Cf.Release(blank);
        }
    }
}
