using Augram.Platform.Windows.Input;
using Xunit;

namespace Augram.Platform.Windows.Tests.Input;

public sealed class Win32CursorProbeTests
{
    [Fact]
    public void ReturnsAPositionOnAnInteractiveDesktop()
    {
        var probe = new Win32CursorProbe();

        var ok = probe.TryGetPosition(out var x, out var y);

        if (!Environment.UserInteractive)
        {
            return;
        }

        Assert.True(ok);
        Assert.InRange(x, -65_536, 65_536);
        Assert.InRange(y, -65_536, 65_536);
    }
}
