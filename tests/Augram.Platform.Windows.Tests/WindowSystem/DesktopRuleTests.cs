using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>The desktop is recognised by its root class, wherever on the chain it appears.</summary>
public sealed class DesktopRuleTests
{
    [Fact]
    public void IconViewUnderProgman_IsDesktop()
    {
        Assert.True(DesktopRule.IsDesktop(["SysListView32", "SHELLDLL_DefView", "Progman"]));
    }

    [Fact]
    public void IconViewUnderWorkerW_IsDesktop()
    {
        Assert.True(DesktopRule.IsDesktop(["SysListView32", "SHELLDLL_DefView", "WorkerW"]));
    }

    [Fact]
    public void ExplorerFileView_IsNotDesktop()
    {
        Assert.False(DesktopRule.IsDesktop(["DirectUIHWND", "SHELLDLL_DefView", "CabinetWClass"]));
    }

    [Fact]
    public void EmptyChain_IsNotDesktop()
    {
        Assert.False(DesktopRule.IsDesktop([]));
    }
}
