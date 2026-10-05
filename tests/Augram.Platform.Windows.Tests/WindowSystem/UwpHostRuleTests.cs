using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>UWP frames resolve to their CoreWindow child; everything else resolves to itself.</summary>
public sealed class UwpHostRuleTests
{
    private const nint Frame = 0x100;
    private const nint Core = 0x101;

    [Fact]
    public void PointOnFrameBorder_ResolvesToCoreWindow()
    {
        var win = new FakeWin32()
            .AddWindow(Frame, UwpHostRule.FrameClass, pid: 10)
            .AddWindow(Core, UwpHostRule.CoreWindowClass, parent: Frame, root: Frame, pid: 20);

        Assert.Equal(Core, UwpHostRule.ProcessWindow(win, Frame, Frame, UwpHostRule.FrameClass));
    }

    [Fact]
    public void HiddenCoreWindow_IsSkippedForTheVisibleOne()
    {
        var win = new FakeWin32()
            .AddWindow(Frame, UwpHostRule.FrameClass, pid: 10)
            .AddWindow(0x102, UwpHostRule.CoreWindowClass, parent: Frame, root: Frame, pid: 30, visible: false)
            .AddWindow(Core, UwpHostRule.CoreWindowClass, parent: Frame, root: Frame, pid: 20);

        Assert.Equal(Core, UwpHostRule.ProcessWindow(win, Frame, Frame, UwpHostRule.FrameClass));
    }

    [Fact]
    public void FrameWithoutCoreWindow_KeepsTheHandle()
    {
        var win = new FakeWin32().AddWindow(Frame, UwpHostRule.FrameClass, pid: 10);

        Assert.Equal(Frame, UwpHostRule.ProcessWindow(win, Frame, Frame, UwpHostRule.FrameClass));
    }

    [Fact]
    public void OrdinaryWindow_KeepsTheHandle()
    {
        var win = new FakeWin32().AddWindow(0x200, "Chrome_WidgetWin_1", pid: 10);

        Assert.Equal(0x200, UwpHostRule.ProcessWindow(win, 0x200, 0x200, "Chrome_WidgetWin_1"));
    }
}
