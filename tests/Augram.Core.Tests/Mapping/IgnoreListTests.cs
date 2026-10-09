using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>The ignore list's two rules: which app claims the window under the pointer (both modes), which pauses Augram while focused (only "disable while focused").</summary>
public sealed class IgnoreListTests
{
    private static readonly IgnoredApp Blender = new(GroupId.New(), "Blender", IsActive: true, ByProcess("blender.exe"), DisableEntirely: false);
    private static readonly IgnoredApp VMware = new(GroupId.New(), "VMware", IsActive: true, ByProcess("vmware.exe"), DisableEntirely: true);
    private static readonly IgnoredApp Spine = new(GroupId.New(), "Spine", IsActive: false, ByProcess("spine.exe"), DisableEntirely: true);

    private static MappingDocument Mapping(params IgnoredApp[] ignored) => new([AppGroup.EmptyGlobal], ignored);

    [Fact]
    public void Under_IsAnyActiveAppInEitherMode()
    {
        var mapping = Mapping(Blender, VMware, Spine);

        Assert.Same(Blender, IgnoreList.Under(mapping, Window("blender.exe"), HostPlatform.Windows));
        Assert.Same(VMware, IgnoreList.Under(mapping, Window("vmware.exe"), HostPlatform.Windows));
        Assert.Null(IgnoreList.Under(mapping, Window("spine.exe"), HostPlatform.Windows));
        Assert.Null(IgnoreList.Under(mapping, Window("chrome.exe"), HostPlatform.Windows));
        Assert.Null(IgnoreList.Under(mapping, null, HostPlatform.Windows));
    }

    [Fact]
    public void PausedBy_IsOnlyAnActiveDisableWhileFocusedApp()
    {
        var mapping = Mapping(Blender, VMware, Spine);

        Assert.Same(VMware, IgnoreList.PausedBy(mapping, Window("vmware.exe"), HostPlatform.Windows));
        Assert.Null(IgnoreList.PausedBy(mapping, Window("blender.exe"), HostPlatform.Windows));
        Assert.Null(IgnoreList.PausedBy(mapping, Window("spine.exe"), HostPlatform.Windows));
        Assert.Null(IgnoreList.PausedBy(mapping, null, HostPlatform.Windows));
    }

    [Fact]
    public void Watches_FollowActiveEntriesThatCanMatchHere()
    {
        Assert.False(IgnoreList.WatchesPointer(Mapping(), HostPlatform.Windows));
        Assert.False(IgnoreList.WatchesPointer(Mapping(Spine), HostPlatform.Windows));
        Assert.True(IgnoreList.WatchesPointer(Mapping(Blender), HostPlatform.Windows));
        Assert.False(IgnoreList.WatchesFocus(Mapping(Blender), HostPlatform.Windows));
        Assert.True(IgnoreList.WatchesFocus(Mapping(Blender, VMware), HostPlatform.Windows));
        Assert.True(IgnoreList.WatchesPointer(Mapping(VMware), HostPlatform.Windows));
    }

    [Fact]
    public void AnEntryWithNothingForThisPlatform_IsNotWatchedThere()
    {
        // vmware.exe has no known macOS name: on a Mac the entry can match nothing, so nothing is watched.
        Assert.False(IgnoreList.CanMatchOn(VMware.Matcher, HostPlatform.MacOS));
        Assert.False(IgnoreList.WatchesPointer(Mapping(VMware), HostPlatform.MacOS));
        Assert.False(IgnoreList.WatchesFocus(Mapping(VMware), HostPlatform.MacOS));

        var resolve = VMware with { Matcher = VMware.Matcher with { MacProcessNames = ["VMware Fusion"] } };
        Assert.True(IgnoreList.WatchesFocus(Mapping(resolve), HostPlatform.MacOS));
        Assert.Same(resolve, IgnoreList.PausedBy(Mapping(resolve), Window("VMware Fusion"), HostPlatform.MacOS));

        // A guessed name counts, and a matcher on the title alone matches on every platform.
        Assert.True(IgnoreList.CanMatchOn(ByProcess("chrome.exe"), HostPlatform.MacOS));
        Assert.True(IgnoreList.CanMatchOn(new AppMatcher { Title = "Chimera" }, HostPlatform.MacOS));
        Assert.False(IgnoreList.CanMatchOn(AppMatcher.Empty, HostPlatform.Windows));
    }
}
