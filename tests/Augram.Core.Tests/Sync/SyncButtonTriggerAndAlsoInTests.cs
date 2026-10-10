using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;
using Augram.Core.Sync;
using Augram.Core.Tests.Mapping.Support;
using Augram.Core.Tests.Sync.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync;

/// <summary>
/// Button triggers and a command's "Also in" in the sync (sync format 14, plan 0005): both travel in the command item, an own
/// button trigger in the own-steps item; an "Also in" naming an Exclusions › Global entry the merge removed (or switched to
/// disable-while-focused, or moved to Per command) is dropped when the merged document is validated, never a conflict or a
/// failed merge.
/// </summary>
public sealed class SyncButtonTriggerAndAlsoInTests : TwoMachineTest
{
    private static readonly Gesture Anchor = SyncSamples.NewGesture("Anchor");
    private static readonly StepRegistry Registry = new([FakeStepType.Instance, RemapStepType.Instance]);
    private static readonly Trigger RightLeft = Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right));

    private static IgnoredApp Excluded(string name)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false);

    [Fact]
    public void ACommandItemCarriesItsButtonTriggerRemapStepAndAlsoIn()
    {
        var blender = Excluded("Blender");
        var remap = new CommandStep(new RemapStep(new RemapOutput.Key(KeyCode.X, KeyModifiers.Shift | KeyModifiers.Meta)), HostPlatform.Windows);
        var magnifier = NewCommand("Magnifier", RightLeft, remap) with { AlsoIn = [blender.Id] };

        var item = new SyncItem.CommandItem(GroupId.Global, magnifier);

        Assert.Contains("\"trigger\":{\"button\":\"Left\",\"hold\":{\"buttons\":\"Right\"}}", item.Content, StringComparison.Ordinal);
        Assert.Contains($"\"alsoIn\":[\"{blender.Id}\"]", item.Content, StringComparison.Ordinal);
        var back = Assert.IsType<SyncItem.CommandItem>(SyncItem.Parse(item.Key, item.Content, Registry));
        Assert.Equal(magnifier.Trigger, back.Command.Trigger);
        Assert.Equal(magnifier.AlsoIn, back.Command.AlsoIn);
        Assert.Equal(remap.Step, Assert.Single(back.Command.Steps).Step);
        Assert.Equal(item.Content, back.Content);
    }

    [Fact]
    public void AnOwnStepsItemCarriesAnOwnButtonTrigger()
    {
        var own = Trigger.ForButton(MouseButton.X2, new TriggerHold(HeldButtons.X1));
        var forward = NewCommand("Forward", Trigger.ForButton(MouseButton.Right, new TriggerHold(HeldButtons.Left)), NewStep("forward"))
            .WithTriggerFor(HostPlatform.MacOS, own, DateTimeOffset.UnixEpoch);

        var item = new SyncItem.VersionItem(forward.Id, forward.Name, forward.OwnVersion!);

        Assert.Contains("\"trigger\":{\"button\":\"X2\",\"hold\":{\"buttons\":\"X1\"}}", item.Content, StringComparison.Ordinal);
        var back = Assert.IsType<SyncItem.VersionItem>(SyncItem.Parse(item.Key, item.Content, Registry));
        Assert.Equal(own, back.Version.Trigger);
        Assert.Equal(item.Content, back.Content);
    }

    /// <summary>One side deletes Blender, the other allows the Magnifier in it: the deletion stands and the "Also in" drops it, either way round.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnAlsoInNamingAnEntryDeletedOnTheOtherSide_IsDroppedByTheMerge_WithoutAConflict(bool deletedHere)
    {
        var blender = Excluded("Blender");
        var magnifier = NewCommand("Magnifier", RightLeft, NewStep("loupe"));
        var both = Document([NewGlobal(magnifier)], blender);
        var deleted = Document([NewGlobal(magnifier)]);
        var allowed = Document([NewGlobal(magnifier with { AlsoIn = [blender.Id] })], blender);

        var result = deletedHere
            ? ThreeWayMerge.Merge(Items(both), Items(deleted), Items(allowed))
            : ThreeWayMerge.Merge(Items(both), Items(allowed), Items(deleted));

        Assert.Empty(result.Conflicts);
        Assert.Empty(result.Mapping.Ignored);
        Assert.Empty(result.Mapping.Global.FindCommand(magnifier.Id)!.AlsoIn);
    }

    /// <summary>
    /// One side switches Blender to "disable while focused" or moves it to Per command, the other allows the Magnifier in it:
    /// the change stands and the "Also in" drops it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnAlsoInNamingAnEntryNoLongerPlainGlobalOnTheOtherSide_IsDroppedByTheMerge_WithoutAConflict(bool disableEntirely)
    {
        var blender = Excluded("Blender");
        var changedBlender = disableEntirely ? blender with { DisableEntirely = true } : blender with { Scope = IgnoreScope.PerCommand };
        var magnifier = NewCommand("Magnifier", RightLeft, NewStep("loupe"));
        var both = Document([NewGlobal(magnifier)], blender);
        var changed = Document([NewGlobal(magnifier)], changedBlender);
        var allowed = Document([NewGlobal(magnifier with { AlsoIn = [blender.Id] })], blender);

        var result = ThreeWayMerge.Merge(Items(both), Items(changed), Items(allowed));

        Assert.Empty(result.Conflicts);
        var merged = result.Mapping.Ignored.Single();
        Assert.Equal((changedBlender.Scope, changedBlender.DisableEntirely), (merged.Scope, merged.DisableEntirely));
        Assert.Empty(result.Mapping.Global.FindCommand(magnifier.Id)!.AlsoIn);
    }

    [Fact]
    public void AButtonTriggerAndAnAlsoInMadeOnOneMachine_ArriveOnTheOther()
    {
        var (work, home) = Joined();
        var blender = work.Mapping.AddIgnored(Excluded("Blender"));
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Magnifier", RightLeft, NewStep("loupe")) with { AlsoIn = [blender.Id] });
        work.Sync();

        Assert.Equal(SyncStatus.Applied, home.Sync().Status);

        var arrived = home.Command("Magnifier");
        Assert.Equal(RightLeft, arrived.Trigger);
        Assert.Equal([blender.Id], arrived.AlsoIn);
        Assert.True(work.SameAs(home));

        home.Mapping.UpdateCommand(GroupId.Global, arrived with { AlsoIn = [] });
        home.Sync();
        work.Sync();

        Assert.Empty(work.Command("Magnifier").AlsoIn);
        Assert.True(work.SameAs(home));
    }

    [Fact]
    public void BlenderDeletedOnOneMachineAndAllowedOnTheOther_BothEndWithoutIt()
    {
        var (work, home) = Joined();
        var blender = work.Mapping.AddIgnored(Excluded("Blender"));
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Magnifier", RightLeft, NewStep("loupe")));
        work.Sync();
        home.Sync();

        work.Mapping.RemoveIgnored(blender.Id);
        home.Mapping.UpdateCommand(GroupId.Global, home.Command("Magnifier") with { AlsoIn = [blender.Id] });
        Assert.Empty(work.Sync().Conflicts);
        var report = home.Sync();

        Assert.Empty(report.Conflicts);
        Assert.Null(home.Mapping.FindIgnored(blender.Id));
        Assert.Empty(home.Command("Magnifier").AlsoIn);
        work.Sync();
        Assert.True(work.SameAs(home));
    }

    [Fact]
    public void AFileWithAButtonTriggerIsFormatFourteenOrLater_ABuildThatReadsUpToThirteenPausesOnIt()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        var blender = work.Mapping.AddIgnored(Excluded("Blender"));
        work.Mapping.AddCommand(GroupId.Global, NewCommand("Magnifier", RightLeft, NewStep("loupe")) with { AlsoIn = [blender.Id] });
        work.Sync();
        var file = Remote.Files[work.Id.ToString("D")];

        var header = SyncFileSerializer.ReadHeader(file)!;

        Assert.True(SyncFile.CurrentFormatVersion >= 14);
        Assert.True(ConfigDocument.CurrentSchemaVersion >= 7);
        Assert.Contains("\"button\": \"Left\"", file, StringComparison.Ordinal);
        Assert.Contains("\"alsoIn\": [", file, StringComparison.Ordinal);
        Assert.True(header.IsNewerThan(schemaVersion: 6, formatVersion: 13));
        Assert.True(header.IsNewerThan(ConfigDocument.CurrentSchemaVersion, formatVersion: 13));
        Assert.False(header.IsNewer);
    }

    private static MappingDocument Document(AppGroup[] groups, params IgnoredApp[] ignored) => new(groups, ignored);

    private static SyncItemSet Items(MappingDocument mapping) => MergeScenario.Set([Anchor], mapping);
}
