using Augram.App.Tests.Support;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>The Not in dialog's Add app… (plan 0004 step 7): what a picked window becomes, named free among the whole ignore list.</summary>
public sealed class NotInEditViewModelTests
{
    [Fact]
    public void APickedWindow_BecomesAPerCommandEntryNamedAfterItsExecutable_FreeAmongTheIgnoreList()
    {
        // Joel's unticked Spine on Ignored › Global takes the name; it claims the window but, inactive, stops nothing.
        var oldSpine = new IgnoredApp(GroupId.New(), "Spine", IsActive: false, new AppMatcher { WindowsProcessNames = ["Spine.exe"] }, DisableEntirely: false);
        var edit = new NotInEditViewModel("Zoom in", [oldSpine], [], HostPlatform.Windows);

        var added = edit.AddApp(FakeWindowSystem.Window("Spine.exe", "Spine 4.2"));

        Assert.Equal(("Spine (2)", true, true, false), (added.Name, added.IsPerCommand, added.IsActive, added.DisableEntirely));
        Assert.Equal(["Spine.exe"], added.Matcher.WindowsProcessNames);
        Assert.Equal([added], edit.Added);
        Assert.Equal([added], edit.Entries);
        Assert.Equal([added.Id], edit.NotIn);
        Assert.Equal("Added 'Spine (2)' (Spine.exe), ticked.", edit.AddStatus);
    }

    [Fact]
    public void AnActiveGlobalEntryClaimingTheWindow_IsMentioned()
    {
        var vmware = new IgnoredApp(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);
        var edit = new NotInEditViewModel("Zoom in", [vmware], [], HostPlatform.Windows);

        var added = edit.AddApp(FakeWindowSystem.Window("vmware.exe"));

        // Names compare as the rules compare them: "vmware" is taken by "VMware".
        Assert.Equal("vmware (2)", added.Name);
        Assert.Equal("Added 'vmware (2)' (vmware.exe), ticked. 'VMware' on Exclusions › Global stops all of Augram over it already.", edit.AddStatus);
    }

    [Fact]
    public void OnAMac_TheAppsNameIsTakenAsItIs_IntoTheMacNames()
    {
        var edit = new NotInEditViewModel("Zoom in", [], [], HostPlatform.MacOS);

        var added = edit.AddApp(FakeWindowSystem.Window("Eyeris"));

        Assert.Equal("Eyeris", added.Name);
        Assert.Equal(["Eyeris"], added.Matcher.MacProcessNames);
        Assert.Empty(added.Matcher.WindowsProcessNames);
    }
}
