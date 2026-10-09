using Augram.Platform.MacOS.WindowSystem;
using Xunit;

namespace Augram.Platform.MacOS.Tests.WindowSystem;

/// <summary>The cheap key under the pointer for the ignore list's watch: a hit test on a window list copy at most 250 ms old.</summary>
public sealed class MacWindowKeysTests
{
    private static readonly MacRect Display = new(0, 0, 1512, 982);

    [Fact]
    public void KeyAt_HitTestsTheCopy_AndRereadsItOnlyWhenOld()
    {
        var now = 0L;
        var windows = new List<MacWindowInfo> { Window(7, pid: 1, new MacRect(0, 0, 400, 300)) };
        var keys = new MacWindowKeys(() => (windows.ToArray(), [Display]), () => now, ownProcessId: 100);

        Assert.Equal(7u, keys.KeyAt(10, 10));
        Assert.Equal(0u, keys.KeyAt(900, 900));
        Assert.Equal(1, keys.Reads);

        // A window that opened since the copy is not seen until the copy is old enough.
        windows.Insert(0, Window(9, pid: 2, new MacRect(0, 0, 100, 100)));
        now = 249;
        Assert.Equal(7u, keys.KeyAt(10, 10));
        now = 250;
        Assert.Equal(9u, keys.KeyAt(10, 10));
        Assert.Equal(2, keys.Reads);
    }

    [Fact]
    public void Invalidate_MakesTheNextKeyReadTheList()
    {
        var windows = new List<MacWindowInfo> { Window(7, pid: 1, Display) };
        var keys = new MacWindowKeys(() => (windows.ToArray(), [Display]), () => 0, ownProcessId: 100);

        Assert.Equal(7u, keys.KeyAt(10, 10));
        windows.Insert(0, Window(9, pid: 2, new MacRect(0, 0, 100, 100)));
        keys.Invalidate();

        Assert.Equal(9u, keys.KeyAt(10, 10));
        Assert.Equal(2, keys.Reads);
    }

    private static MacWindowInfo Window(uint id, int pid, MacRect bounds) => new(id, pid, "App" + pid, null, 0, 1, bounds);
}
