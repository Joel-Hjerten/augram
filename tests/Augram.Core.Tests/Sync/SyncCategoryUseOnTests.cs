using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Mapping.Support;
using Augram.Core.Tests.Sync.Support;
using Xunit;

namespace Augram.Core.Tests.Sync;

/// <summary>
/// A category's "Use on" (Joel, 2026-10-08) in the sync: part of the category item, so a change travels; written only when
/// set, so a category on every platform reads as before; and the reason the sync format went to 3, so a build that reads
/// format 2 pauses on a file this build wrote instead of publishing the category back as used everywhere.
/// </summary>
public sealed class SyncCategoryUseOnTests : TwoMachineTest
{
    [Fact]
    public void ACategoryItemCarriesItsUseOnOnlyWhenSet_AndReadsItBack()
    {
        var category = MappingFixtures.NewCategory("Personal");

        var everywhere = new SyncItem.CategoryItem(GroupId.Global, category);
        var pcOnly = new SyncItem.CategoryItem(GroupId.Global, category with { UseOn = PlatformSet.Windows });

        Assert.Equal($"{{\"group\":\"{GroupId.Global}\",\"id\":\"{category.Id}\",\"name\":\"Personal\"}}", everywhere.Content);
        Assert.Equal(everywhere.Key, pcOnly.Key);
        Assert.NotEqual(everywhere.Content, pcOnly.Content);
        Assert.Contains("\"useOn\":[\"windows\"]", pcOnly.Content, StringComparison.Ordinal);
        var back = Assert.IsType<SyncItem.CategoryItem>(SyncItem.Parse(pcOnly.Key, pcOnly.Content, FakeStepType.Registry));
        Assert.Equal(PlatformSet.Windows, back.Category.UseOn);
        Assert.Equal(pcOnly.Content, back.Content);
    }

    [Fact]
    public void ACategorysUseOnSetOnOneMachineArrivesOnTheOther_ItsCommandsUntouched()
    {
        var (work, home) = Joined();
        var global = work.Mapping.Global;
        var window = global.Categories.Single(category => category.Name == "Window");
        work.Mapping.UpdateGroup(global with { Categories = [window with { UseOn = PlatformSet.Windows }] });
        work.Sync();

        var report = home.Sync();

        Assert.Equal(SyncStatus.Applied, report.Status);
        Assert.Equal(PlatformSet.Windows, home.Mapping.Global.FindCategory(window.Id)!.UseOn);
        Assert.Equal(PlatformSet.All, home.Command("Minimize").UseOn);
        Assert.False(home.Mapping.Global.IsCommandUsedOn(home.Command("Minimize"), HostPlatform.MacOS));
        Assert.True(work.SameAs(home));
    }

    [Fact]
    public void AFileThisBuildWritesIsFormatThree_ABuildThatReadsUpToFormatTwoPausesOnIt()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        var global = work.Mapping.Global;
        work.Mapping.UpdateGroup(global with { Categories = [global.Categories[0] with { UseOn = PlatformSet.MacOS }] });
        work.Sync();
        var file = Remote.Files[work.Id.ToString("D")];

        var header = SyncFileSerializer.ReadHeader(file)!;

        Assert.Equal(3, SyncFile.CurrentFormatVersion);
        Assert.Contains("\"formatVersion\": 3", file, StringComparison.Ordinal);
        Assert.Contains("\"useOn\": [", file, StringComparison.Ordinal);
        Assert.Equal(3, header.FormatVersion);
        Assert.False(header.IsNewer);
        Assert.True(header.IsNewerThan(ConfigDocument.CurrentSchemaVersion, formatVersion: 2));
        Assert.False(header.IsNewerThan(ConfigDocument.CurrentSchemaVersion, formatVersion: 3));
    }
}
