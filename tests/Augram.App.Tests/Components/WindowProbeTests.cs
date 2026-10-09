using Augram.App.Components.WindowFinder;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>What the window finder asks the window system: identities read only when the cheap key changes (or at most every 50 ms without one), a fresh read at the release, never one of Augram's own windows as a pick.</summary>
public sealed class WindowProbeTests
{
    private static readonly WindowIdentity Chrome = FakeWindowSystem.Window("chrome.exe", "Google Chrome", handle: 0x100);
    private static readonly WindowIdentity Notepad = FakeWindowSystem.Window("notepad.exe", "Untitled - Notepad", handle: 0x200);

    [Fact]
    public void WhileTheKeyStaysTheSame_TheIdentityIsReadOnce()
    {
        var windows = new FakeWindowSystem().Around(100, 100, Chrome).Around(300, 100, Notepad);
        var probe = new WindowProbe(windows, ownProcessId: 1);

        Assert.Same(Chrome, probe.Hover(90, 90));
        Assert.Same(Chrome, probe.Hover(110, 120));
        Assert.Equal(1, windows.WindowAtCalls);

        Assert.Same(Notepad, probe.Hover(300, 100));
        Assert.Null(probe.Hover(500, 500));
        Assert.Same(Chrome, probe.Hover(100, 100));
        Assert.Equal(3, windows.WindowAtCalls);
    }

    [Fact]
    public void WithoutACheapKey_TheIdentityIsReadAtMostEvery50Milliseconds()
    {
        var now = 1_000L;
        var windows = new FakeWindowSystem { HasKeys = false }.Around(100, 100, Chrome).Around(300, 100, Notepad);
        var probe = new WindowProbe(windows, ownProcessId: 1, () => now);

        Assert.Same(Chrome, probe.Hover(100, 100));
        now += 10;
        Assert.Same(Chrome, probe.Hover(300, 100));
        Assert.Equal(1, windows.WindowAtCalls);

        now += (long)WindowProbe.UnkeyedInterval.TotalMilliseconds;
        Assert.Same(Notepad, probe.Hover(300, 100));
        Assert.Equal(2, windows.WindowAtCalls);
    }

    [Fact]
    public void TheReleaseReadsAfresh_AndNeverPicksAnOwnWindow()
    {
        var own = FakeWindowSystem.Window("Augram.App.exe", "Augram", handle: 0x900, processId: 77);
        var windows = new FakeWindowSystem().Around(100, 100, Chrome).Around(300, 100, own);
        var probe = new WindowProbe(windows, ownProcessId: 77);

        Assert.Same(own, probe.Hover(300, 100));
        Assert.True(probe.IsOwn(own));
        Assert.Null(probe.Pick(300, 100));
        Assert.Null(probe.Pick(500, 500));
        _ = probe.Hover(100, 100);
        Assert.Same(Chrome, probe.Pick(100, 100));
        Assert.Equal(5, windows.WindowAtCalls);
    }
}
