using Augram.Core.Abstractions;
using Augram.Platform.Windows.WindowSystem;
using Xunit;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>A20: activate only when the target root differs from the foreground root and is not the desktop.</summary>
public sealed class ActivationPolicyTests
{
    [Fact]
    public void TargetRootIsForegroundRoot_NotNeeded()
    {
        Assert.False(ActivationPolicy.NeedsActivation(Identity(0x10), 0x10));
    }

    [Fact]
    public void TargetIsDesktop_NotNeeded()
    {
        Assert.False(ActivationPolicy.NeedsActivation(Identity(0x10, isDesktop: true), 0x20));
    }

    [Fact]
    public void DifferentRoot_Needed()
    {
        Assert.True(ActivationPolicy.NeedsActivation(Identity(0x10), 0x20));
    }

    [Fact]
    public void NoForeground_Needed()
    {
        Assert.True(ActivationPolicy.NeedsActivation(Identity(0x10), 0));
    }

    [Fact]
    public void PopupOverItsOwner_NotNeeded()
    {
        // A find dialog (child handle 0x11) owned by the editor (root owner 0x10) that already has focus.
        Assert.False(ActivationPolicy.NeedsActivation(Identity(0x10, handle: 0x11), 0x10));
    }

    private static WindowIdentity Identity(nint root, nint handle = 0, bool isDesktop = false)
        => new(handle == 0 ? root : handle, root, "app.exe", null, null, [], 1, false, isDesktop);
}
