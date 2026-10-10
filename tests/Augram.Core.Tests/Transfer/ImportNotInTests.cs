using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Transfer;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;
using static Augram.Core.Tests.Transfer.Support.TransferSamples;

namespace Augram.Core.Tests.Transfer;

/// <summary>
/// A Global command's "Not in" and its own drag distance through export and import (plan 0004 step 2): both travel in the file;
/// the "Not in" follows the file's app groups to the local ones they match by name, keeps a new group's id, and names nothing
/// that is not in the result (an export without the group leaves it out).
/// </summary>
public sealed class ImportNotInTests
{
    private static readonly Trigger RightWheelDownAt3 = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right, DragDistancePx: 3));

    [Fact]
    public void ANotInNamingAnExportedGroup_FollowsItToTheLocalGroupOfThatName()
    {
        var theirs = WithZoom(SampleConfig());
        var mine = WithZoom(SampleConfig(), notIn: false);
        var file = Exported(theirs, ExportScope.Of([GroupId.Global, Id(theirs, "Spine"), Id(theirs, "Eyeris")]));

        var plan = ImportPlan.Create(file, mine);

        Assert.Equal(ImportStatus.Same, Entry(plan, SyncItemKey.ForGroup(Id(mine, "Spine"))).Status);
        var zoom = CommandNamed(plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs }).Mapping, AppGroup.GlobalName, "Zoom In");
        Assert.Equal(new[] { Id(mine, "Spine"), Id(mine, "Eyeris") }.OrderBy(id => id.Value), zoom.NotIn);
        Assert.Equal(3, zoom.Trigger.Hold.DragDistancePx);
    }

    [Fact]
    public void TheSameSetupFromAMachineThatNeverShared_HasNothingToImport()
    {
        var mine = WithZoom(SampleConfig());
        var theirs = WithZoom(SampleConfig());

        var plan = ImportPlan.Create(Exported(theirs, ExportScope.Everything), mine);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void IntoAConfigurationWithoutTheGroups_TheyArriveWithTheirIds_AndTheNotInNamesThem()
    {
        var theirs = WithZoom(SampleConfig());

        var result = ImportPlan.Create(Exported(theirs, ExportScope.Everything), Empty()).Preview;

        Assert.Equal(Contents(theirs), Contents(result));
        Assert.Equal(CommandNamed(theirs.Mapping, AppGroup.GlobalName, "Zoom In").NotIn, CommandNamed(result.Mapping, AppGroup.GlobalName, "Zoom In").NotIn);
    }

    [Fact]
    public void ANotInNamingAGroupNotExported_IsLeftOut_AndTheImportNamesNoGroupOfMine()
    {
        var theirs = WithZoom(SampleConfig());
        var mine = WithZoom(SampleConfig(), notIn: false);

        var file = Exported(theirs, ExportScope.Of([GroupId.Global]));
        var result = ImportPlan.Create(file, mine).Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs });

        Assert.Empty(CommandNamed(file.MappingOrEmpty, AppGroup.GlobalName, "Zoom In").NotIn);
        var zoom = CommandNamed(result.Mapping, AppGroup.GlobalName, "Zoom In");
        Assert.Empty(zoom.NotIn);
        Assert.Equal(3, zoom.Trigger.Hold.DragDistancePx);
    }

    /// <summary>The sample configuration plus app groups Spine and Eyeris and Global's Zoom In on Right + wheel down at 3 px, not in both (unless <paramref name="notIn"/> is false).</summary>
    private static ConfigDocument WithZoom(ConfigDocument document, bool notIn = true)
    {
        var spine = NewGroup("Spine", ByProcess("spine.exe"));
        var eyeris = NewGroup("Eyeris", ByProcess("eyeris.exe"));
        var zoom = NewCommand("Zoom In", RightWheelDownAt3, NewStep("zoom")) with { NotIn = notIn ? [spine.Id, eyeris.Id] : [] };
        var groups = document.Mapping.Groups.Select(group => group.IsGlobal ? group with { Commands = [.. group.Commands, zoom] } : group);
        return document with { Mapping = MappingRules.ValidDocument(document.Mapping with { Groups = [.. groups, spine, eyeris] }) };
    }

    private static GroupId Id(ConfigDocument document, string group) => GroupNamed(document.Mapping, group).Id;

    private static ImportEntry Entry(ImportPlan plan, SyncItemKey key) => plan.Entries.Single(entry => entry.Key == key);
}
