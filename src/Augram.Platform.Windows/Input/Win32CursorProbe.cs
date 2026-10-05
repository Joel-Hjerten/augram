using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Augram.Platform.Windows.Interop;

namespace Augram.Platform.Windows.Input;

/// <summary>The <see cref="ICursorProbe"/> over <c>GetCursorPos</c>, in physical pixels (PerMonitorV2); the hook watchdog polls it once a second.</summary>
[SupportedOSPlatform("windows")]
public sealed class Win32CursorProbe : ICursorProbe
{
    public bool TryGetPosition(out int x, out int y)
    {
        var ok = NativeMethods.GetCursorPos(out var point);
        x = ok ? point.X : 0;
        y = ok ? point.Y : 0;
        return ok;
    }
}
