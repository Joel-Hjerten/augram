using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Input;

/// <summary>The <see cref="ICursorProbe"/> over a blank <c>CGEvent</c>'s location, in global top-left points like the hook; the hook watchdog polls it once a second.</summary>
[SupportedOSPlatform("macos")]
public sealed class MacCursorProbe : ICursorProbe
{
    public bool TryGetPosition(out int x, out int y)
    {
        var cgEvent = MacNative.CGEventCreate(0);
        if (cgEvent == 0)
        {
            x = 0;
            y = 0;
            return false;
        }

        try
        {
            var location = MacNative.CGEventGetLocation(cgEvent);
            x = (int)Math.Round(location.X);
            y = (int)Math.Round(location.Y);
            return true;
        }
        finally
        {
            Cf.Release(cgEvent);
        }
    }
}
