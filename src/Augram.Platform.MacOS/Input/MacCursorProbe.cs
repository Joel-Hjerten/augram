using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Interop;

namespace Augram.Platform.MacOS.Input;

/// <summary>The <see cref="ICursorProbe"/> over <see cref="MacPointer"/> (a blank <c>CGEvent</c>'s location), in global top-left points like the hook, rounded; the hook watchdog polls it once a second.</summary>
[SupportedOSPlatform("macos")]
public sealed class MacCursorProbe : ICursorProbe
{
    public bool TryGetPosition(out int x, out int y)
    {
        if (!MacPointer.TryRead(out var location))
        {
            x = 0;
            y = 0;
            return false;
        }

        x = (int)Math.Round(location.X);
        y = (int)Math.Round(location.Y);
        return true;
    }
}
