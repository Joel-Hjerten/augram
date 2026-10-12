using Augram.Core.Abstractions;
using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>A20 over top-level windows: activate only when the one under the gesture start is not the one in front, and is not the desktop.</summary>
public sealed class ActivationPolicyTests
{
    private const nint Main = 0x10;
    private const nint OwnedPopup = 0x11;
    private const nint Other = 0x20;

    [Fact]
    public void TargetIsTheForegroundWindow_NotNeeded()
    {
        Assert.False(ActivationPolicy.NeedsActivation(Identity(Main), Main, Main));
    }

    [Fact]
    public void TargetIsDesktop_NotNeeded()
    {
        Assert.False(ActivationPolicy.NeedsActivation(Identity(Main, isDesktop: true), Main, Other));
    }

    [Fact]
    public void DifferentWindowInFront_Needed()
    {
        Assert.True(ActivationPolicy.NeedsActivation(Identity(Main), Main, Other));
    }

    [Fact]
    public void NoForeground_Needed()
    {
        Assert.True(ActivationPolicy.NeedsActivation(Identity(Main), Main, 0));
    }

    [Fact]
    public void GestureOverTheFocusedFindDialog_NotNeeded()
    {
        // A find dialog owned by the editor has focus and the gesture is drawn over it: its own top-level window is in front, so it keeps focus.
        Assert.False(ActivationPolicy.NeedsActivation(Identity(Main, handle: OwnedPopup), OwnedPopup, OwnedPopup));
    }

    [Fact]
    public void OwnedHelperWindowInFront_Needed()
    {
        // Plan 0008: after a minimize Windows put PotPlayer's title-less helper window (owned by the main window) in front.
        // Same root owner, different top-level window: the main window under the gesture must be activated or the keys go to the helper.
        Assert.True(ActivationPolicy.NeedsActivation(Identity(Main), Main, OwnedPopup));
    }

    [Fact]
    public void TargetDisabledByItsModalDialog_NotNeeded()
    {
        Assert.False(ActivationPolicy.NeedsActivation(Identity(Main), Main, OwnedPopup, targetEnabled: false));
    }

    private static WindowIdentity Identity(nint root, nint handle = 0, bool isDesktop = false)
        => new(handle == 0 ? root : handle, root, "app.exe", null, null, [], 1, false, isDesktop);
}
