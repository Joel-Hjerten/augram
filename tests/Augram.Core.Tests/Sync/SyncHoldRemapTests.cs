using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Augram.Core.Tests.Mapping.Support;
using Augram.Core.Tests.Sync.Support;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync;

/// <summary>
/// Hold remaps in the sync (F9, sync format 11): a hold remap's header is an item of its own, keyed like a category
/// (<c>holdRemap:&lt;groupId&gt;/&lt;id&gt;</c>); the commands under it travel as command items carrying <c>holdRemap</c>. The
/// three-way merge of a hold remap item is the merge table (<see cref="ThreeWayMergeTests"/>, every kind); these are the
/// hold-remap commands, the repairs and two machines end to end. Steps are fake steps (the machines read with the fake registry).
/// </summary>
public sealed class SyncHoldRemapTests : TwoMachineTest
{
    private static readonly Gesture Anchor = SyncSamples.NewGesture("Anchor");

    [Fact]
    public void AHoldRemapItemIsItsHeaderWithItsGroup_KeyedLikeACategory()
    {
        var space = HoldRemap.For(KeyCode.Space);
        var blender = GroupId.New();

        var item = new SyncItem.HoldRemapItem(blender, space);
        var pcOnly = new SyncItem.HoldRemapItem(blender, space with { UseOn = PlatformSet.Windows });

        Assert.Equal($"holdRemap:{blender.Value:D}/{space.Id.Value:D}", item.Key.ToString());
        Assert.Equal(item.Key, SyncItemKey.Parse(item.Key.ToString()));
        Assert.Equal($"{{\"group\":\"{blender}\",\"id\":\"{space.Id}\",\"name\":\"Space\",\"holdKey\":\"Space\",\"tapTimeMs\":180,\"isActive\":true}}", item.Content);
        Assert.Contains("\"useOn\":[\"windows\"]", pcOnly.Content, StringComparison.Ordinal);
        var back = Assert.IsType<SyncItem.HoldRemapItem>(SyncItem.Parse(pcOnly.Key, pcOnly.Content, FakeStepType.Registry));
        Assert.Equal((blender, space with { UseOn = PlatformSet.Windows }), (back.GroupId, back.HoldRemap));
    }

    [Fact]
    public void AGroupItemLeavesItsHoldRemapsToTheirOwnItems_ACommandItemCarriesItsHoldRemap()
    {
        var (space, group) = Blender();
        var items = Items(group);

        var header = Assert.IsType<SyncItem.GroupItem>(items.Find(SyncItemKey.ForGroup(group.Id)));
        Assert.Empty(header.Header.HoldRemaps);
        Assert.DoesNotContain("holdRemap", header.Content, StringComparison.Ordinal);
        Assert.NotNull(items.Find(SyncItemKey.ForHoldRemap(group.Id, space.Id)));
        var orbit = items.OfType<SyncItem.CommandItem>().Single(item => item.Name == "Orbit");
        Assert.Contains($"\"holdRemap\":\"{space.Id}\"", orbit.Content, StringComparison.Ordinal);
        Assert.Contains("\"input\":{\"buttons\":\"Left\"}", orbit.Content, StringComparison.Ordinal);
        Assert.Equal(new SyncKindCounts(1, 0, 0), SyncCounts.Between(Items(group with { HoldRemaps = [], Commands = [] }), items).HoldRemaps);
    }

    [Fact]
    public void ACommandUnderAHoldRemapChangedThereIsTaken_StillUnderIt()
    {
        var (space, group) = Blender();
        var orbit = group.Commands.Single(command => command.Name == "Orbit");
        var theirs = group with { Commands = [.. group.Commands.Select(command => command == orbit ? command with { Trigger = Trigger.ForInput(HoldInput.Of(MouseButton.X1)) } : command)] };

        var result = ThreeWayMerge.Merge(Items(group), Items(group), Items(theirs));

        var merged = result.Mapping.Groups[1].FindCommand(orbit.Id)!;
        Assert.Equal((space.Id, Trigger.ForInput(HoldInput.Of(MouseButton.X1))), (merged.HoldRemapId, merged.Trigger));
        Assert.Empty(result.Conflicts);
        Assert.Empty(result.Repairs);
    }

    [Fact]
    public void ACommandAddedHereUnderAHoldRemapDeletedThere_BecomesOrdinaryWithoutItsInput_WithARepair()
    {
        var (space, group) = Blender();
        var added = Command(space, "Spin", HoldInput.Of(MouseButton.X2));
        var mine = group with { Commands = [.. group.Commands, added] };
        var theirs = group with { HoldRemaps = [], Commands = [] };

        var result = ThreeWayMerge.Merge(Items(group), Items(mine), Items(theirs));

        var blender = result.Mapping.Groups[1];
        Assert.Empty(blender.HoldRemaps);
        var spin = Assert.Single(blender.Commands);
        Assert.Equal((added.Id, null, Trigger.None), (spin.Id, spin.HoldRemapId, (Trigger)spin.Trigger));
        var repair = Assert.Single(result.Repairs);
        Assert.Equal(SyncRepairKind.HoldRemapCleared, repair.Kind);
        Assert.Equal("Command 'Spin' in 'Blender' is now an ordinary command without its input: its hold remap is gone.", repair.Description);
    }

    [Fact]
    public void TwoMachinesAddingASpaceHoldRemapToOneGroup_KeepBoth_TheIncomingOneRenamedWithoutAHoldKey()
    {
        var blender = NewGroup("Blender", ByProcess("blender.exe"));
        var mineSpace = HoldRemap.For(KeyCode.Space);
        var theirSpace = HoldRemap.For(KeyCode.Space);
        var mine = blender with { HoldRemaps = [mineSpace], Commands = [Command(mineSpace, "Orbit", HoldInput.Of(MouseButton.Left))] };
        var theirs = blender with { HoldRemaps = [theirSpace], Commands = [Command(theirSpace, "Pan", HoldInput.Of(MouseButton.Left))] };

        var result = ThreeWayMerge.Merge(Items(blender), Items(mine), Items(theirs));

        var merged = result.Mapping.Groups[1];
        Assert.Equal([("Space", KeyCode.Space), ("Space (2)", KeyCode.None)], merged.HoldRemaps.Select(holdRemap => (holdRemap.Name, holdRemap.HoldKey)));
        Assert.All(merged.Commands, command => Assert.IsType<Trigger.InputTrigger>(command.Trigger));
        Assert.Equal([SyncRepairKind.Renamed, SyncRepairKind.HoldKeyCleared], result.Repairs.Select(repair => repair.Kind));
        Assert.Equal("Incoming hold remap 'Space (2)' in 'Blender' has no hold key now: 'Space' already uses Space.", result.Repairs[1].Description);
    }

    [Fact]
    public void AHoldKeyChangedHereThatAnInputAddedThereNowUses_UnbindsTheIncomingCommand()
    {
        var (space, group) = Blender();
        var mine = group with { HoldRemaps = [space with { HoldKey = KeyCode.T }] };
        var theirs = group with { Commands = [.. group.Commands, Command(space, "Tilt", new HoldInput.Key(KeyCode.T))] };

        var result = ThreeWayMerge.Merge(Items(group), Items(mine), Items(theirs));

        var tilt = result.Mapping.Groups[1].Commands.Single(command => command.Name == "Tilt");
        Assert.Same(Trigger.None, tilt.Trigger);
        Assert.Equal(space.Id, tilt.HoldRemapId);
        var repair = Assert.Single(result.Repairs);
        Assert.Equal(SyncRepairKind.Unbound, repair.Kind);
        Assert.Equal("Command 'Tilt' under 'Space' in 'Blender' unbound: 'Tilt' cannot use T as its input: it is the hold key of 'Space'.", repair.Description);
    }

    /// <summary>Command names are unique within their parent (Joel, 2026-10-10): a clash under one hold remap is renamed, one across parents is not a clash.</summary>
    [Fact]
    public void TwoMachinesAddingASpinUnderOneHoldRemap_TheIncomingOneIsRenamed()
    {
        var (space, group) = Blender();
        var mine = group with { Commands = [.. group.Commands, Command(space, "Spin", HoldInput.Of(MouseButton.X1))] };
        var theirs = group with { Commands = [.. group.Commands, Command(space, "SPIN", HoldInput.Of(MouseButton.X2))] };

        var result = ThreeWayMerge.Merge(Items(group), Items(mine), Items(theirs));

        var spins = result.Mapping.Groups[1].Commands.Where(command => command.Name.StartsWith("Spin", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(["Spin", "SPIN (2)"], spins.Select(command => command.Name));
        Assert.All(spins, command => Assert.Equal(space.Id, command.HoldRemapId));
        var repair = Assert.Single(result.Repairs);
        Assert.Equal(SyncRepairKind.Renamed, repair.Kind);
        Assert.Equal("Incoming command 'SPIN' under 'Space' in 'Blender' renamed 'SPIN (2)': the name is taken.", repair.Description);
    }

    [Fact]
    public void TwoMachinesAddingASpinUnderDifferentParents_KeepTheName()
    {
        var (space, group) = Blender();
        var s = HoldRemap.For(KeyCode.S);
        var both = group with { HoldRemaps = [space, s] };
        var mine = both with { Commands = [.. group.Commands, Command(space, "Spin", HoldInput.Of(MouseButton.X1))] };
        var theirs = both with { Commands = [.. group.Commands, Command(s, "Spin", HoldInput.Of(MouseButton.X1)), NewCommand("Spin")] };

        var result = ThreeWayMerge.Merge(Items(both), Items(mine), Items(theirs));

        var spins = result.Mapping.Groups[1].Commands.Where(command => command.Name == "Spin").Select(command => command.HoldRemapId).ToList();
        Assert.Equal(3, spins.Count);
        Assert.Equal(new HashSet<HoldRemapId?> { space.Id, s.Id, null }, spins.ToHashSet());
        Assert.Empty(result.Repairs);
    }

    [Fact]
    public void AHoldRemapAndItsCommandsMadeOnOneMachineArriveOnTheOther_ThenAnEditFollows()
    {
        var (work, home) = Joined();
        var (space, group) = Blender();
        work.Mapping.AddGroup(group);
        work.Sync();

        var report = home.Sync();

        Assert.Equal(SyncStatus.Applied, report.Status);
        Assert.Equal(new SyncKindCounts(1, 0, 0), report.Counts.HoldRemaps);
        var arrived = home.Mapping.FindGroup(group.Id)!;
        Assert.Equal(space, Assert.Single(arrived.HoldRemaps));
        var sent = work.Mapping.FindGroup(group.Id)!;
        Assert.Equal(sent.Commands.Select(command => (command.Name, command.HoldRemapId, command.Trigger)), arrived.Commands.Select(command => (command.Name, command.HoldRemapId, command.Trigger)));
        Assert.True(work.SameAs(home));

        home.Mapping.UpdateHoldRemap(group.Id, space with { TapTimeMs = 150 });
        home.Sync();
        work.Sync();

        Assert.Equal(150, work.Mapping.FindGroup(group.Id)!.HoldRemaps.Single().TapTimeMs);
        Assert.True(work.SameAs(home));
    }

    [Fact]
    public void AFileWithHoldRemapsIsFormatElevenOrLater_ABuildThatReadsUpToTenPausesOnIt()
    {
        var work = Machine("PC-WORK", SyncSamples.Setup());
        work.Mapping.AddGroup(Blender().Group);
        work.Sync();
        var file = Remote.Files[work.Id.ToString("D")];

        var header = SyncFileSerializer.ReadHeader(file)!;

        Assert.True(SyncFile.CurrentFormatVersion >= 11);
        Assert.Contains("\"holdRemaps\": [", file, StringComparison.Ordinal);
        Assert.True(header.IsNewerThan(schemaVersion: 3, formatVersion: 10));
        Assert.True(header.IsNewerThan(ConfigDocument.CurrentSchemaVersion, formatVersion: 10));
        Assert.False(header.IsNewer);
    }

    /// <summary>Blender with a Space hold remap and three Steps commands under it (fake steps: the machines read with the fake registry).</summary>
    private static (HoldRemap Space, AppGroup Group) Blender()
    {
        var space = HoldRemap.For(KeyCode.Space);
        var group = NewGroup("Blender", ByProcess("blender.exe"),
            Command(space, "Orbit", HoldInput.Of(MouseButton.Left)),
            Command(space, "Zoom both", HoldInput.Of(MouseButton.Left, MouseButton.Right)),
            Command(space, "Grab", new HoldInput.Key(KeyCode.W)));
        return (space, group with { HoldRemaps = [space] });
    }

    private static Command Command(HoldRemap holdRemap, string name, HoldInput input)
        => NewCommand(name, Trigger.ForInput(input), NewStep(name)) with { HoldRemapId = holdRemap.Id };

    private static SyncItemSet Items(AppGroup blender) => MergeScenario.Set([Anchor], new MappingDocument([AppGroup.EmptyGlobal, blender], []));
}
