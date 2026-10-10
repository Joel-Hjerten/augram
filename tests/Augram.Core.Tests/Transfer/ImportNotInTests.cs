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
/// A command's "Not in", the Ignored › Per command entries it names and its own drag distance through export and import (plan
/// 0004): an export takes the entries its commands name, selected or not; the "Not in" of every group's commands follows the
/// file's entries to the local ones they match by name, keeps a new entry's id, and names nothing that is not in the result.
/// </summary>
public sealed class ImportNotInTests
{
    private static readonly Trigger RightWheelDownAt3 = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right, DragDistancePx: 3));

    [Fact]
    public void ANotInNamingAnExportedEntry_FollowsItToTheLocalEntryOfThatName_InEveryGroup()
    {
        var theirs = WithZoom(SampleConfig());
        var mine = WithZoom(SampleConfig(), notIn: false);
        var file = Exported(theirs, ExportScope.Everything);

        var plan = ImportPlan.Create(file, mine);

        var spine = Entry(plan, SyncItemKey.ForIgnored(Ignored(mine, "Spine")));
        Assert.Equal(ImportStatus.Same, spine.Status);
        Assert.Equal(ImportMatchKind.Name, spine.Match?.By);
        var result = plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs }).Mapping;
        var zoom = CommandNamed(result, AppGroup.GlobalName, "Zoom In");
        Assert.Equal(new[] { Ignored(mine, "Spine"), Ignored(mine, "Eyeris") }.OrderBy(id => id.Value), zoom.NotIn);
        Assert.Equal(3, zoom.Trigger.Hold.DragDistancePx);
        Assert.Equal([Ignored(mine, "Spine")], CommandNamed(result, "Chrome", "Chrome zoom").NotIn);
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
    public void IntoAConfigurationWithoutTheEntries_TheyArriveWithTheirIds_AndTheNotInNamesThem()
    {
        var theirs = WithZoom(SampleConfig());

        var result = ImportPlan.Create(Exported(theirs, ExportScope.Everything), Empty()).Preview;

        Assert.Equal(Contents(theirs), Contents(result));
        Assert.Equal(CommandNamed(theirs.Mapping, AppGroup.GlobalName, "Zoom In").NotIn, CommandNamed(result.Mapping, AppGroup.GlobalName, "Zoom In").NotIn);
        Assert.Equal(IgnoreScope.PerCommand, result.Mapping.Ignored.Single(app => app.Name == "Spine").Scope);
    }

    /// <summary>Plan 0004 step 2's known edge, gone: Global alone, no ignored app selected, still carries its "Not in".</summary>
    [Fact]
    public void ExportingGlobalAlone_TakesThePerCommandEntriesItsCommandsName_AndNoOtherIgnoredApp()
    {
        var theirs = WithZoom(SampleConfig());
        var mine = WithZoom(SampleConfig(), notIn: false);

        var file = Exported(theirs, ExportScope.Of([GroupId.Global]));
        var result = ImportPlan.Create(file, mine).Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs });

        Assert.Equal(["Eyeris", "Spine"], file.MappingOrEmpty.Ignored.Select(app => app.Name).Order(StringComparer.Ordinal));
        Assert.All(file.MappingOrEmpty.Ignored, app => Assert.Equal(IgnoreScope.PerCommand, app.Scope));
        Assert.Equal(2, TransferContents.Of(file).IgnoredApps);
        Assert.Equal(CommandNamed(theirs.Mapping, AppGroup.GlobalName, "Zoom In").NotIn, CommandNamed(file.MappingOrEmpty, AppGroup.GlobalName, "Zoom In").NotIn);
        var zoom = CommandNamed(result.Mapping, AppGroup.GlobalName, "Zoom In");
        Assert.Equal(new[] { Ignored(mine, "Spine"), Ignored(mine, "Eyeris") }.OrderBy(id => id.Value), zoom.NotIn);
        Assert.Equal(3, zoom.Trigger.Hold.DragDistancePx);
    }

    [Fact]
    public void AnAppGroupExportedAlone_TakesTheEntryItsCommandNames()
    {
        var theirs = WithZoom(SampleConfig());

        var file = Exported(theirs, ExportScope.Of([GroupNamed(theirs.Mapping, "Chrome").Id]));

        Assert.Equal("Spine", file.MappingOrEmpty.Ignored.Single().Name);
        Assert.Equal([file.MappingOrEmpty.Ignored.Single().Id], CommandNamed(file.MappingOrEmpty, "Chrome", "Chrome zoom").NotIn);
    }

    /// <summary>Names are unique across both lists, so the file's Per command Spine matches a Spine here on Ignored › Global.</summary>
    [Fact]
    public void AnEntryOfThatNameOnIgnoredGlobalHere_IsAConflict_KeepMineDropsTheTick()
    {
        var theirs = WithZoom(SampleConfig());
        var mine = WithZoom(SampleConfig(), notIn: false);
        mine = mine with
        {
            Mapping = MappingRules.ValidDocument(mine.Mapping with
            {
                Ignored = [.. mine.Mapping.Ignored.Select(app => app.Name == "Spine" ? app with { Scope = IgnoreScope.Global } : app)],
            }),
        };

        var plan = ImportPlan.Create(Exported(theirs, ExportScope.Everything), mine);
        var key = SyncItemKey.ForIgnored(Ignored(mine, "Spine"));
        var kept = plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs, Items = new Dictionary<SyncItemKey, SyncChoice> { [key] = SyncChoice.KeepMine } }).Mapping;
        var taken = plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs }).Mapping;

        Assert.Equal(ImportStatus.Different, Entry(plan, key).Status);
        Assert.Equal([Ignored(mine, "Eyeris")], CommandNamed(kept, AppGroup.GlobalName, "Zoom In").NotIn);
        Assert.Equal(new[] { Ignored(mine, "Spine"), Ignored(mine, "Eyeris") }.OrderBy(id => id.Value), CommandNamed(taken, AppGroup.GlobalName, "Zoom In").NotIn);
    }

    /// <summary>
    /// The sample configuration plus the Per command entries Spine, Eyeris and Unused; Global's Zoom In on Right + wheel down at 3
    /// px, not in Spine and Eyeris, and Chrome's Chrome zoom not in Spine (unless <paramref name="notIn"/> is false).
    /// </summary>
    private static ConfigDocument WithZoom(ConfigDocument document, bool notIn = true)
    {
        var spine = PerCommand("Spine");
        var eyeris = PerCommand("Eyeris");
        var unused = PerCommand("Unused");
        var zoom = NewCommand("Zoom In", RightWheelDownAt3, NewStep("zoom")) with { NotIn = notIn ? [spine.Id, eyeris.Id] : [] };
        var chromeZoom = NewCommand("Chrome zoom", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), NewStep("chrome")) with { NotIn = notIn ? [spine.Id] : [] };
        var groups = document.Mapping.Groups.Select(group => group.IsGlobal ? group with { Commands = [.. group.Commands, zoom] }
            : group.Name == "Chrome" ? group with { Commands = [.. group.Commands, chromeZoom] }
            : group);
        return document with { Mapping = MappingRules.ValidDocument(new MappingDocument([.. groups], [.. document.Mapping.Ignored, spine, eyeris, unused])) };
    }

    private static IgnoredApp PerCommand(string name)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };

    private static GroupId Ignored(ConfigDocument document, string name) => document.Mapping.Ignored.Single(app => app.Name == name).Id;

    private static ImportEntry Entry(ImportPlan plan, SyncItemKey key) => plan.Entries.Single(entry => entry.Key == key);
}
