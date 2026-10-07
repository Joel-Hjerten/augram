using Augram.Core.Abstractions;
using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>
/// The adapter's contract and the state operations (close, minimize, maximize/restore, always-on-top): exact call
/// sequence over the fake and failure reasons with error codes. Placement is in <see cref="Win32WindowOperationsPlacementTests"/>.
/// </summary>
public sealed class Win32WindowOperationsTests
{
    private const nint Root = 0x10;
    private const nint Child = 0x11;
    private const int SwMaximize = 3;
    private const int SwMinimize = 6;
    private const int SwRestore = 9;

    public static TheoryData<WindowOperation> AllOperations => new(Enum.GetValues<WindowOperation>());

    [Fact]
    public void Platform_IsWindows()
    {
        Assert.Equal(HostPlatform.Windows, Operations(new FakeWin32()).Platform);
    }

    [Theory]
    [MemberData(nameof(AllOperations))]
    public void Supports_EveryOperation(WindowOperation operation)
    {
        Assert.True(Operations(new FakeWin32()).Supports(operation));
    }

    [Theory]
    [MemberData(nameof(AllOperations))]
    public void WindowGone_FailsBeforeTouchingAnything(WindowOperation operation)
    {
        var win = new FakeWin32();

        var result = Operations(win).Perform(operation, Identity(), new WindowSize(640, 480));

        Assert.False(result.Succeeded);
        Assert.Equal("window gone", result.Reason);
        Assert.Empty(win.Calls);
    }

    [Fact]
    public void Close_PostsSysCommandCloseToTheRoot()
    {
        var win = Setup();

        var result = Operations(win).Perform(WindowOperation.Close, Identity(handle: Child));

        Assert.True(result.Succeeded);
        Assert.Equal(["close:16"], win.Calls);
    }

    [Fact]
    public void Close_PostFails_ReasonCarriesTheErrorCode()
    {
        var win = Setup();
        win.PostFails = true;
        win.Error = 5;

        var result = Operations(win).Perform(WindowOperation.Close, Identity());

        Assert.False(result.Succeeded);
        Assert.Equal("PostMessage(SC_CLOSE) failed (5)", result.Reason);
    }

    [Fact]
    public void Minimize_ShowsMinimize()
    {
        var win = Setup();

        var result = Operations(win).Perform(WindowOperation.Minimize, Identity());

        Assert.True(result.Succeeded);
        Assert.Equal([$"show:16:{SwMinimize}"], win.Calls);
    }

    [Fact]
    public void MaximizeOrRestore_NotZoomed_Maximizes()
    {
        var win = Setup();

        Operations(win).Perform(WindowOperation.MaximizeOrRestore, Identity());

        Assert.Equal([$"show:16:{SwMaximize}"], win.Calls);
    }

    [Fact]
    public void MaximizeOrRestore_Zoomed_Restores()
    {
        var win = Setup(zoomed: true);

        Operations(win).Perform(WindowOperation.MaximizeOrRestore, Identity());

        Assert.Equal([$"show:16:{SwRestore}"], win.Calls);
    }

    [Fact]
    public void ToggleAlwaysOnTop_NotTopmost_SetsTopmost()
    {
        var win = Setup();

        var result = Operations(win).Perform(WindowOperation.ToggleAlwaysOnTop, Identity());

        Assert.True(result.Succeeded);
        Assert.Equal(["topmost:16:True"], win.Calls);
        Assert.True(win.IsTopmost(Root));
    }

    [Fact]
    public void ToggleAlwaysOnTop_Topmost_ClearsIt()
    {
        var win = Setup(topmost: true);

        Operations(win).Perform(WindowOperation.ToggleAlwaysOnTop, Identity());

        Assert.Equal(["topmost:16:False"], win.Calls);
        Assert.False(win.IsTopmost(Root));
    }

    [Fact]
    public void ToggleAlwaysOnTop_SetWindowPosFails_ReasonCarriesTheErrorCode()
    {
        var win = Setup();
        win.SetWindowPosFails = true;
        win.Error = 1400;

        var result = Operations(win).Perform(WindowOperation.ToggleAlwaysOnTop, Identity());

        Assert.Equal("SetWindowPos(topmost) failed (1400)", result.Reason);
    }

    private static FakeWin32 Setup(bool zoomed = false, bool topmost = false)
        => new FakeWin32()
            .AddWindow(Root, "App", pid: 1)
            .AddWindow(Child, "Child", parent: Root, root: Root, pid: 1)
            .WithState(Root, zoomed: zoomed, topmost: topmost);

    private static Win32WindowOperations Operations(FakeWin32 win) => new(win, win);

    private static WindowIdentity Identity(nint handle = Root) => new(handle, Root, "app.exe", null, null, [], 1, false, false);
}
