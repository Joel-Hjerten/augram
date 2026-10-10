using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Augram.Core.Sync;
using Augram.Core.Transfer;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;
using static Augram.Core.Tests.Transfer.Support.TransferSamples;

namespace Augram.Core.Tests.Transfer;

/// <summary>
/// A button trigger, its Remap step and a command's "Also in" through export and import (plan 0005): an export takes the
/// Exclusions › Global entries its commands' "Also in" names, selected or not; the "Also in" of every group's commands follows
/// the file's entries to the local ones they match by name, keeps a new entry's id, and names nothing that is not in the result.
/// </summary>
public sealed class ImportAlsoInTests
{
    private static readonly Trigger RightLeft = Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right));
    private static readonly RemapStep WinShiftX = new(new RemapOutput.Key(KeyCode.X, KeyModifiers.Shift | KeyModifiers.Meta));

    [Fact]
    public void AnAlsoInNamingAnExportedEntry_FollowsItToTheLocalEntryOfThatName_InEveryGroup()
    {
        var theirs = WithMagnifier(SampleConfig());
        var mine = WithMagnifier(SampleConfig(), alsoIn: false);
        var file = Exported(theirs, ExportScope.Everything);

        var plan = ImportPlan.Create(file, mine);

        var blender = Entry(plan, SyncItemKey.ForIgnored(Ignored(mine, "Blender")));
        Assert.Equal(ImportStatus.Same, blender.Status);
        Assert.Equal(ImportMatchKind.Name, blender.Match?.By);
        var result = plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs }).Mapping;
        var magnifier = CommandNamed(result, AppGroup.GlobalName, "Magnifier");
        Assert.Equal([Ignored(mine, "Blender")], magnifier.AlsoIn);
        Assert.Equal(RightLeft, magnifier.Trigger);
        Assert.Equal(WinShiftX, Assert.Single(magnifier.Steps).Step);
        Assert.Equal([Ignored(mine, "Blender")], CommandNamed(result, "Blender", "Blender zoom").AlsoIn);
    }

    [Fact]
    public void TheSameSetupFromAMachineThatNeverShared_HasNothingToImport()
    {
        var mine = WithMagnifier(SampleConfig());
        var theirs = WithMagnifier(SampleConfig());

        var plan = ImportPlan.Create(Exported(theirs, ExportScope.Everything), mine);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void IntoAConfigurationWithoutTheEntry_ItArrivesWithItsId_AndTheAlsoInNamesIt()
    {
        var theirs = WithMagnifier(SampleConfig());

        var result = ImportPlan.Create(Exported(theirs, ExportScope.Everything), Empty()).Preview;

        Assert.Equal(Contents(theirs), Contents(result));
        Assert.Equal(CommandNamed(theirs.Mapping, AppGroup.GlobalName, "Magnifier").AlsoIn, CommandNamed(result.Mapping, AppGroup.GlobalName, "Magnifier").AlsoIn);
    }

    [Fact]
    public void ExportingGlobalAlone_TakesTheGlobalEntriesItsCommandsAreAlsoIn_AndNoOtherIgnoredApp()
    {
        var theirs = WithMagnifier(SampleConfig());
        var mine = WithMagnifier(SampleConfig(), alsoIn: false);

        var file = Exported(theirs, ExportScope.Of([GroupId.Global]));
        var result = ImportPlan.Create(file, mine).Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs });

        var entry = Assert.Single(file.MappingOrEmpty.Ignored);
        Assert.Equal(("Blender", IgnoreScope.Global, false), (entry.Name, entry.Scope, entry.DisableEntirely));
        Assert.Equal(1, TransferContents.Of(file).IgnoredApps);
        Assert.Equal([entry.Id], CommandNamed(file.MappingOrEmpty, AppGroup.GlobalName, "Magnifier").AlsoIn);
        Assert.Equal([Ignored(mine, "Blender")], CommandNamed(result.Mapping, AppGroup.GlobalName, "Magnifier").AlsoIn);
    }

    [Fact]
    public void AnAppGroupExportedAlone_TakesTheEntryItsCommandIsAlsoIn()
    {
        var theirs = WithMagnifier(SampleConfig());

        var file = Exported(theirs, ExportScope.Of([GroupNamed(theirs.Mapping, "Blender").Id]));

        Assert.Equal("Blender", file.MappingOrEmpty.Ignored.Single().Name);
        Assert.Equal([file.MappingOrEmpty.Ignored.Single().Id], CommandNamed(file.MappingOrEmpty, "Blender", "Blender zoom").AlsoIn);
    }

    /// <summary>The file's plain Blender matches a Blender here that disables Augram while focused: that ignored item is Different.</summary>
    [Fact]
    public void AnEntryOfThatNameHereThatDisablesWhileFocused_IsAConflict_KeepMineDropsTheAlsoIn()
    {
        var theirs = WithMagnifier(SampleConfig());
        var mine = WithMagnifier(SampleConfig(), alsoIn: false);
        mine = mine with
        {
            Mapping = MappingRules.ValidDocument(mine.Mapping with
            {
                Ignored = [.. mine.Mapping.Ignored.Select(app => app.Name == "Blender" ? app with { DisableEntirely = true } : app)],
            }),
        };

        var plan = ImportPlan.Create(Exported(theirs, ExportScope.Everything), mine);
        var key = SyncItemKey.ForIgnored(Ignored(mine, "Blender"));
        var kept = plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs, Items = new Dictionary<SyncItemKey, SyncChoice> { [key] = SyncChoice.KeepMine } }).Mapping;
        var taken = plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs }).Mapping;

        Assert.Equal(ImportStatus.Different, Entry(plan, key).Status);
        Assert.Empty(CommandNamed(kept, AppGroup.GlobalName, "Magnifier").AlsoIn);
        Assert.Equal([Ignored(mine, "Blender")], CommandNamed(taken, AppGroup.GlobalName, "Magnifier").AlsoIn);
    }

    /// <summary>
    /// The sample configuration plus the plain Exclusions › Global entries Blender and Resolve; Global's Magnifier (Left while
    /// holding Right, one Remap step to Win+Shift+X) and the Blender group's Blender zoom (Right + wheel up), both also in
    /// Blender (unless <paramref name="alsoIn"/> is false).
    /// </summary>
    private static ConfigDocument WithMagnifier(ConfigDocument document, bool alsoIn = true)
    {
        var blender = Excluded("Blender");
        var resolve = Excluded("Resolve");
        var magnifier = NewCommand("Magnifier", RightLeft, new CommandStep(WinShiftX, HostPlatform.Windows)) with { AlsoIn = alsoIn ? [blender.Id] : [] };
        var blenderZoom = NewCommand("Blender zoom", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), NewStep("zoom")) with { AlsoIn = alsoIn ? [blender.Id] : [] };
        var groups = document.Mapping.Groups.Select(group => group.IsGlobal ? group with { Commands = [.. group.Commands, magnifier] }
            : group.Name == "Blender" ? group with { Commands = [.. group.Commands, blenderZoom] }
            : group);
        return document with { Mapping = MappingRules.ValidDocument(new MappingDocument([.. groups], [.. document.Mapping.Ignored, blender, resolve])) };
    }

    private static IgnoredApp Excluded(string name)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), DisableEntirely: false);

    private static GroupId Ignored(ConfigDocument document, string name) => document.Mapping.Ignored.Single(app => app.Name == name).Id;

    private static ImportEntry Entry(ImportPlan plan, SyncItemKey key) => plan.Entries.Single(entry => entry.Key == key);
}
