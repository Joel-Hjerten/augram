using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Sync;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Sync;

/// <summary>F8: a command's own steps are a sync item of their own (Joel, 2026-10-07), so each machine edits its side without conflicts.</summary>
public sealed class SyncOwnVersionTests
{
    private static readonly DateTimeOffset Then = new(2026, 10, 7, 20, 0, 0, TimeSpan.Zero);
    private static readonly CommandStep CtrlBackspace = new(new HotkeyStep(KeyModifiers.Control, KeyCode.Backspace), HostPlatform.Windows);
    private static readonly CommandStep CtrlShiftBackspace = new(new HotkeyStep(KeyModifiers.Control | KeyModifiers.Shift, KeyCode.Backspace), HostPlatform.Windows);
    private static readonly CommandStep OptBackspace = new(new HotkeyStep(KeyModifiers.Alt, KeyCode.Backspace), HostPlatform.MacOS);
    private static readonly CommandStep OptShiftBackspace = new(new HotkeyStep(KeyModifiers.Alt | KeyModifiers.Shift, KeyCode.Backspace), HostPlatform.MacOS);

    [Fact]
    public void ACommandWithOwnStepsIsTwoItems_TheCommandWithoutThemAndTheStepsKeyedByTheCommand()
    {
        var command = DeleteWord().WithStepsFor(HostPlatform.MacOS, [OptBackspace], Then);

        var items = SyncItemSet.From([], Document(command));

        var commandItem = Assert.IsType<SyncItem.CommandItem>(items.Find(SyncItemKey.ForCommand(command.Id)));
        Assert.DoesNotContain("ownVersion", commandItem.Content, StringComparison.Ordinal);
        Assert.Null(commandItem.Command.OwnVersion);
        var versionItem = Assert.IsType<SyncItem.VersionItem>(items.Find(SyncItemKey.ForCommandVersion(command.Id)));
        Assert.Equal("Delete word (macOS steps)", versionItem.Name);
        Assert.Equal($"version:{command.Id.Value:D}", versionItem.Key.ToString());
        Assert.Equal(versionItem.Key, SyncItemKey.Parse(versionItem.Key.ToString()));

        var parsed = Assert.IsType<SyncItem.VersionItem>(SyncItem.Parse(versionItem.Key, versionItem.Content, StepRegistry.BuiltIn));
        Assert.Equal(versionItem.Content, parsed.Content);
        Assert.Equal([OptBackspace], parsed.Version.Steps);
    }

    [Fact]
    public void TheOriginalChangedOnOneMachineAndTheOwnStepsOnTheOther_MergeWithoutAConflict()
    {
        var shared = DeleteWord().WithStepsFor(HostPlatform.MacOS, [OptBackspace], Then);
        var onMac = shared.WithStepsFor(HostPlatform.MacOS, [OptShiftBackspace], Then.AddMinutes(5));
        var onPc = shared.WithStepsFor(HostPlatform.Windows, [CtrlShiftBackspace], Then.AddMinutes(6));

        var result = ThreeWayMerge.Merge(Items(shared), Items(onMac), Items(onPc));

        Assert.Empty(result.Conflicts);
        var merged = result.Mapping.Global.Commands.Single();
        Assert.Equal([CtrlShiftBackspace], merged.Steps);
        Assert.Equal([OptShiftBackspace], merged.OwnVersion!.Steps);
        Assert.True(merged.IsOwnVersionStale);
        Assert.Equal(1, result.Counts.Commands.Changed);
        Assert.Equal("commands +0 ~1 -0", result.Counts.ToString());
    }

    [Fact]
    public void OwnStepsMadeThereArriveHere_AndDroppingThemThereDropsThemHere()
    {
        var shared = DeleteWord();
        var withMac = shared.WithStepsFor(HostPlatform.MacOS, [OptBackspace], Then);

        var arrived = ThreeWayMerge.Merge(Items(shared), Items(shared), Items(withMac));
        Assert.Equal([OptBackspace], arrived.Mapping.Global.Commands.Single().OwnVersion!.Steps);

        var dropped = ThreeWayMerge.Merge(Items(withMac), Items(withMac), Items(withMac.WithoutOwnVersion()));
        Assert.Null(dropped.Mapping.Global.Commands.Single().OwnVersion);
    }

    [Fact]
    public void OwnStepsWhoseCommandIsGoneAreDroppedWithARepairLine()
    {
        var shared = DeleteWord().WithStepsFor(HostPlatform.MacOS, [OptBackspace], Then);
        var editedHere = shared.WithStepsFor(HostPlatform.MacOS, [OptShiftBackspace], Then.AddMinutes(1));
        var deletedThere = MappingRules.ValidDocument(new MappingDocument([NewGlobal()], []));

        var result = ThreeWayMerge.Merge(Items(shared), Items(editedHere), SyncItemSet.From([], deletedThere));

        Assert.Empty(result.Mapping.Global.Commands);
        var repair = Assert.Single(result.Repairs, repair => repair.Kind == SyncRepairKind.OwnStepsDropped);
        Assert.Equal(SyncItemKey.ForCommandVersion(shared.Id), repair.Key);
    }

    /// <summary>Joel, 2026-10-09: a trigger changed on the Mac is the Mac's own, carried by the own-steps item (format 6).</summary>
    [Fact]
    public void AnOwnTriggerTravelsWithTheOwnStepsItem_AndMergesWithoutAConflict()
    {
        var original = DeleteWord() with { Trigger = Trigger.ForWheel(Augram.Core.Capture.WheelDirection.Up, TriggerHold.WithStroke(KeyModifiers.Control)) };
        var macTrigger = Trigger.ForWheel(Augram.Core.Capture.WheelDirection.Up, TriggerHold.WithStroke(KeyModifiers.Alt));
        var onMac = original.WithTriggerFor(HostPlatform.MacOS, macTrigger, Then);

        var versionItem = Assert.IsType<SyncItem.VersionItem>(Items(onMac).Find(SyncItemKey.ForCommandVersion(onMac.Id)));
        var parsed = Assert.IsType<SyncItem.VersionItem>(SyncItem.Parse(versionItem.Key, versionItem.Content, StepRegistry.BuiltIn));
        Assert.Equal(macTrigger, parsed.Version.Trigger);
        Assert.Equal(KeyModifiers.Control, Assert.IsType<SyncItem.CommandItem>(Items(onMac).Find(SyncItemKey.ForCommand(onMac.Id))).Command.Trigger.Hold.Keys);

        var result = ThreeWayMerge.Merge(Items(original), Items(original with { Name = "Delete word" }), Items(onMac));

        Assert.Empty(result.Conflicts);
        Assert.Equal(macTrigger, Assert.Single(result.Mapping.Global.Commands).TriggerFor(HostPlatform.MacOS));
    }

    private static Command DeleteWord() => new(CommandId.New(), "Delete word", Trigger.None, IsActive: true, [CtrlBackspace]);

    private static MappingDocument Document(Command command) => MappingRules.ValidDocument(new MappingDocument([NewGlobal(command)], []));

    private static SyncItemSet Items(Command command) => SyncItemSet.From([], Document(command));
}
