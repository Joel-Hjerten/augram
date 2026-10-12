using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The Not in dialog's Add app… (plan 0004 step 7): what a picked window becomes, named free among the Per command entries (not the
/// Global ones; Joel, 2026-10-12); and its ✕ on an unused entry (Joel, 2026-10-12: the list kept growing).
/// </summary>
public sealed class NotInEditViewModelTests
{
    [Fact]
    public void APickedWindow_BecomesAPerCommandEntryNamedAfterItsExecutable_KeepingANameAGlobalEntryHas()
    {
        // Joel's unticked Spine on Ignored › Global claims the window but, inactive, stops nothing; the name is free on Per command.
        var oldSpine = new IgnoredApp(GroupId.New(), "Spine", IsActive: false, new AppMatcher { WindowsProcessNames = ["Spine.exe"] }, DisableEntirely: false);
        var edit = new NotInEditViewModel("Zoom in", [oldSpine], [], HostPlatform.Windows);

        var added = edit.AddApp(FakeWindowSystem.WindowOf("Spine.exe", "Spine 4.2"));

        Assert.Equal(("Spine", true, true, false), (added.Name, added.IsPerCommand, added.IsActive, added.DisableEntirely));
        Assert.Equal(["Spine.exe"], added.Matcher.WindowsProcessNames);
        Assert.Equal([added], edit.Added);
        Assert.Equal([added], edit.Entries);
        Assert.Equal([added.Id], edit.NotIn);
        Assert.Equal("Added 'Spine' (Spine.exe), ticked.", edit.AddStatus);
    }

    [Fact]
    public void APickedWindow_TakesTheFirstFreeSuffix_WhenAPerCommandEntryHasItsName()
    {
        // An older Per command "Spine" matching another executable: the names compare as the rules compare them.
        var spine64 = new IgnoredApp(GroupId.New(), "spine", IsActive: true, new AppMatcher { WindowsProcessNames = ["Spine64.exe"] }, DisableEntirely: false) { Scope = IgnoreScope.PerCommand };
        var edit = new NotInEditViewModel("Zoom in", [spine64], [], HostPlatform.Windows);

        var added = edit.AddApp(FakeWindowSystem.WindowOf("Spine.exe", "Spine 4.2"));

        Assert.Equal("Spine (2)", added.Name);
        Assert.Equal("Added 'Spine (2)' (Spine.exe), ticked.", edit.AddStatus);
    }

    [Fact]
    public void AnActiveGlobalEntryClaimingTheWindow_IsMentioned()
    {
        var vmware = new IgnoredApp(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);
        var edit = new NotInEditViewModel("Zoom in", [vmware], [], HostPlatform.Windows);

        var added = edit.AddApp(FakeWindowSystem.WindowOf("vmware.exe"));

        Assert.Equal("vmware", added.Name);
        Assert.Equal("Added 'vmware' (vmware.exe), ticked. 'VMware' on Exclusions › Global stops all of Augram over it already.", edit.AddStatus);
    }

    [Fact]
    public void OnAMac_TheAppsNameIsTakenAsItIs_IntoTheMacNames()
    {
        var edit = new NotInEditViewModel("Zoom in", [], [], HostPlatform.MacOS);

        var added = edit.AddApp(FakeWindowSystem.WindowOf("Eyeris"));

        Assert.Equal("Eyeris", added.Name);
        Assert.Equal(["Eyeris"], added.Matcher.MacProcessNames);
        Assert.Empty(added.Matcher.WindowsProcessNames);
    }

    [Fact]
    public void AnEntryNoOtherCommandNames_IsRemovableWhileUnticked_AndAnAddedOneIsSimplyNotAdded()
    {
        var spine = PerCommand("Spine");
        var eyeris = PerCommand("Eyeris");
        var krita = PerCommand("Krita");
        var edit = new NotInEditViewModel("Zoom in", [spine, eyeris, krita], [spine.Id], HostPlatform.Windows, usedElsewhere: [eyeris.Id]);

        // Spine is ticked here, Eyeris another command's: neither. Krita: no one's.
        Assert.Equal([false, false, true], new[] { spine, eyeris, krita }.Select(app => edit.IsRemovable(app.Id)));
        Assert.False(edit.Remove(eyeris.Id));

        CheckList(edit).Items.Single(item => item.Caption == "Spine").Value.Set(false);
        Assert.True(CheckList(edit).Items.Single(item => item.Caption == "Spine").Remove!.Removable.Get());
        Assert.True(edit.Remove(spine.Id));
        Assert.True(edit.Remove(krita.Id));

        var added = edit.AddApp(FakeWindowSystem.WindowOf("Blender.exe"));
        CheckList(edit).Items.Single(item => item.Caption == "Blender").Value.Set(false);
        Assert.True(edit.Remove(added.Id));

        Assert.Equal(["Eyeris"], edit.Entries.Select(app => app.Name));
        Assert.Equal([spine.Id, krita.Id], edit.Removed);
        Assert.Empty(edit.Added);
        Assert.Empty(edit.NotIn);
    }

    private static CheckListField CheckList(NotInEditViewModel edit)
        => edit.Declare().Sections.SelectMany(section => section.Fields).OfType<CheckListField>().Single();

    private static IgnoredApp PerCommand(string name)
        => new(GroupId.New(), name, IsActive: true, new AppMatcher { WindowsProcessNames = [name + ".exe"] }, DisableEntirely: false) { Scope = IgnoreScope.PerCommand };
}
