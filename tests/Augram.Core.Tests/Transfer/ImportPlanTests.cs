using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Augram.Core.Sync;
using Augram.Core.Tests.Fixtures;
using Augram.Core.Tests.Mapping.Support;
using Augram.Core.Transfer;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;
using static Augram.Core.Tests.Transfer.Support.TransferSamples;

namespace Augram.Core.Tests.Transfer;

/// <summary>
/// The import plan and the choices (plan 0003): the sync's merge with no base, so an item is new, the same, or a conflict that
/// keeps mine until chosen; nothing is ever deleted; the sync's repairs (a taken name " (2)", a taken trigger unbound).
/// </summary>
public sealed class ImportPlanTests
{
    [Fact]
    public void IntoAnEmptyConfiguration_EverythingIsNew_AndArrivesAsInTheFile()
    {
        var source = SampleConfig();

        var plan = ImportPlan.Create(Exported(source, ExportScope.Everything), Empty());

        Assert.All(plan.Entries.Where(entry => entry.Key != SyncItemKey.ForGroup(GroupId.Global)), entry => Assert.Equal(ImportStatus.New, entry.Status));
        Assert.Empty(plan.Conflicts);
        var result = plan.Preview;
        Assert.Equal(Contents(source), Contents(result));
        Assert.Empty(result.Repairs);
        Assert.Equal((new SyncKindCounts(3, 0, 0), new SyncKindCounts(2, 0, 0), new SyncKindCounts(1, 0, 0), new SyncKindCounts(13, 0, 0)), (result.Counts.Gestures, result.Counts.Groups, result.Counts.HoldRemaps, result.Counts.Commands));
        var blender = GroupNamed(result.Mapping, "Blender");
        Assert.Equal("Space", Assert.Single(blender.HoldRemaps).Name);
        Assert.All(blender.Commands, command => Assert.Equal(blender.HoldRemaps[0].Id, command.HoldRemapId));
        Assert.Equal(result.Mapping.Global.Categories.Single().Id, CommandNamed(result.Mapping, AppGroup.GlobalName, "Minimize").CategoryId);
    }

    [Fact]
    public void IntoTheConfigurationItCameFrom_ThereIsNothingToDo_WhateverTheChoices()
    {
        var config = SampleConfig();

        var plan = ImportPlan.Create(Exported(config, ExportScope.Everything), config);

        Assert.True(plan.IsEmpty);
        Assert.All(plan.Entries, entry => Assert.Equal(ImportStatus.Same, entry.Status));
        Assert.True(plan.Preview.IsEmpty);
        Assert.True(plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs, TakeSettings = true }).IsEmpty);
    }

    [Fact]
    public void AFileFromAMachineThatNeverShared_LinesUpByName_AndHasNothingNew()
    {
        var mine = SampleConfig();
        var theirs = SampleConfig();

        var plan = ImportPlan.Create(Exported(theirs, ExportScope.Everything), mine);

        Assert.True(plan.IsEmpty);
        Assert.All(plan.Entries.Where(entry => entry.Key != SyncItemKey.ForGroup(GroupId.Global)), entry => Assert.NotNull(entry.Match));
        Assert.Equal(ImportMatchKind.HoldKey, plan.Entries.Single(entry => entry.Kind == SyncItemKind.HoldRemap).Match!.By);
        Assert.All(plan.Entries.Where(entry => entry.Kind != SyncItemKind.HoldRemap && entry.Match is not null), entry => Assert.Equal(ImportMatchKind.Name, entry.Match!.By));
    }

    [Fact]
    public void ImportingTheSameFileTwice_ChangesNothingTheSecondTime()
    {
        var mine = SampleConfig();
        var theirs = SampleConfig();
        var firefox = NewGroup("Firefox", null, NewCommand("Reload", GestureNamed(theirs.Gestures, "Up").Id, NewStep("f5")));
        theirs = theirs with { Gestures = [.. theirs.Gestures, Flick("Right")], Mapping = MappingRules.ValidDocument(theirs.Mapping with { Groups = [.. theirs.Mapping.Groups, firefox] }) };
        var file = Exported(theirs, ExportScope.Everything);
        using var session = Session(mine);

        Assert.True(ImportPlan.Create(file, session.Document).Preview.ApplyTo(session.Settings, session.Gestures, session.Mapping));

        Assert.Equal(GestureNamed(session.Gestures.All, "Up").Id, ((Trigger.GestureTrigger)CommandNamed(session.Mapping.Current, "Firefox", "Reload").Trigger).GestureId);
        Assert.True(ImportPlan.Create(file, session.Document).IsEmpty);
    }

    [Fact]
    public void AChangedCommand_IsAConflictWithTheFilesName_KeepMineByDefault()
    {
        var (mine, file) = CloseTabChangedThere();

        var plan = ImportPlan.Create(file, mine, new ImportOptions { SourceName = "Chrome.augram.json" });

        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal((SyncItemKind.Command, "Close tab", "Chrome.augram.json"), (conflict.Kind, conflict.Name, conflict.MachineName));
        Assert.Equal(1, plan.Count(ImportStatus.Different));
        Assert.True(plan.Preview.IsEmpty);
    }

    [Fact]
    public void TakeTheirs_PutsTheFilesVersionInPlaceOfMine_UnderMyId()
    {
        var (mine, file) = CloseTabChangedThere();
        var plan = ImportPlan.Create(file, mine);

        var result = plan.Resolve(Choose(plan.Conflicts[0].Key, SyncChoice.TakeTheirs));

        var closeTab = CommandNamed(result.Mapping, "Chrome", "Close tab");
        Assert.Equal(CommandNamed(mine.Mapping, "Chrome", "Close tab").Id, closeTab.Id);
        Assert.Equal("ctrl+f4", ((FakeStep)closeTab.Steps[0].Step).Text);
        Assert.Equal(new SyncKindCounts(0, 1, 0), result.Counts.Commands);
        Assert.Empty(result.Repairs);
    }

    [Fact]
    public void KeepBoth_AddsTheFilesCommandBesideMine_RenamedAndUnboundSinceMineHasItsGesture()
    {
        var (mine, file) = CloseTabChangedThere();
        var plan = ImportPlan.Create(file, mine);

        var result = plan.Resolve(Choose(plan.Conflicts[0].Key, SyncChoice.KeepBoth));

        var chrome = GroupNamed(result.Mapping, "Chrome");
        Assert.Equal(["Close tab", "Close tab (2)"], chrome.Commands.Select(command => command.Name));
        var copy = chrome.Commands[1];
        Assert.Equal((Trigger.None, "ctrl+f4"), (copy.Trigger, ((FakeStep)copy.Steps[0].Step).Text));
        Assert.NotEqual(chrome.Commands[0].Id, copy.Id);
        Assert.Equal([SyncRepairKind.Renamed, SyncRepairKind.Unbound], result.Repairs.Select(repair => repair.Kind));
    }

    [Fact]
    public void KeepBothOnAGroup_TakesTheirs_WithTheSyncsNote()
    {
        var mine = SampleConfig();
        var theirs = WithGroup(mine, "Chrome", chrome => chrome with { SuppressGlobals = true });
        var plan = ImportPlan.Create(Exported(theirs, ExportScope.Of([GroupNamed(mine.Mapping, "Chrome").Id])), mine);

        var result = plan.Resolve(new ImportChoices { Default = SyncChoice.KeepBoth });

        Assert.Equal(SyncItemKind.Group, Assert.Single(plan.Conflicts).Kind);
        Assert.True(GroupNamed(result.Mapping, "Chrome").SuppressGlobals);
        Assert.Equal("Keep both is for gestures and commands; 'Chrome' took the file's version.", Assert.Single(result.Notes));
    }

    [Fact]
    public void AGestureRedrawnThere_KeepMine_BindsTheFilesNewCommandToMine()
    {
        var (mine, file) = UpRedrawnThereAndUsedByANewCommand();
        var plan = ImportPlan.Create(file, mine);

        var result = plan.Preview;

        Assert.Equal(SyncItemKind.Gesture, Assert.Single(plan.Conflicts).Kind);
        var up = GestureNamed(mine.Gestures, "Up");
        Assert.Equal(Trigger.ForGesture(up.Id), CommandNamed(result.Mapping, "Chrome", "Reload").Trigger);
        Assert.Equal(Trigger.ForGesture(up.Id), CommandNamed(result.Mapping, "Chrome", "Reload").OwnVersion!.Trigger);
        Assert.Equal(Contents(mine.Gestures, null), Contents(result.Gestures, null));
    }

    [Fact]
    public void AGestureRedrawnThere_TakeTheirs_GivesMyGestureTheirShape_ForEveryCommand()
    {
        var (mine, file) = UpRedrawnThereAndUsedByANewCommand();
        var plan = ImportPlan.Create(file, mine);
        var up = GestureNamed(mine.Gestures, "Up");

        var result = plan.Resolve(Choose(SyncItemKey.ForGesture(up.Id), SyncChoice.TakeTheirs));

        var taken = GestureNamed(result.Gestures, "Up");
        Assert.Equal(up.Id, taken.Id);
        Assert.Equal(StockFlicks.Template("UpRight"), taken.Samples[0].ToArray());
        Assert.Equal(Trigger.ForGesture(up.Id), CommandNamed(result.Mapping, AppGroup.GlobalName, "Minimize").Trigger);
        Assert.Equal(Trigger.ForGesture(up.Id), CommandNamed(result.Mapping, "Chrome", "Reload").Trigger);
    }

    [Fact]
    public void AGestureRedrawnThere_KeepBoth_AddsTheirs_AndTheFilesCommandsBindToIt_WhileMineKeepMine()
    {
        var (mine, file) = UpRedrawnThereAndUsedByANewCommand();
        var plan = ImportPlan.Create(file, mine);
        var up = GestureNamed(mine.Gestures, "Up");

        var result = plan.Resolve(Choose(SyncItemKey.ForGesture(up.Id), SyncChoice.KeepBoth));

        var theirs = GestureNamed(result.Gestures, "Up (2)");
        Assert.NotEqual(up.Id, theirs.Id);
        Assert.Equal(StockFlicks.Template("UpRight"), theirs.Samples[0].ToArray());
        Assert.Equal(Trigger.ForGesture(theirs.Id), CommandNamed(result.Mapping, "Chrome", "Reload").Trigger);
        Assert.Equal(Trigger.ForGesture(theirs.Id), CommandNamed(result.Mapping, "Chrome", "Reload").OwnVersion!.Trigger);
        Assert.Equal(Trigger.ForGesture(up.Id), CommandNamed(result.Mapping, AppGroup.GlobalName, "Minimize").Trigger);
        Assert.Equal(SyncRepairKind.Renamed, Assert.Single(result.Repairs).Kind);
    }

    [Fact]
    public void AGestureUnderAnotherName_IsMatchedByShapeOnlyWhenAsked()
    {
        var mine = SampleConfig();
        var theirs = SampleConfig();
        theirs = theirs with { Gestures = [.. theirs.Gestures.Select(gesture => gesture.Name == "Up" ? gesture with { Name = "North" } : gesture)] };
        var file = Exported(theirs, ExportScope.Of([GroupId.Global]));

        var byShape = ImportPlan.Create(file, mine, new ImportOptions { MatchShapes = RecognitionOptions.Default });
        var byName = ImportPlan.Create(file, mine);

        var shaped = byShape.Entries.Single(entry => entry.Match?.By == ImportMatchKind.Shape);
        Assert.Equal(("Up", "North", ImportStatus.Different), (shaped.Name, shaped.Match!.FileName, shaped.Status));
        Assert.True(shaped.Match.Score >= ConfusionCheck.DuplicateCutOff);
        Assert.Equal(ImportStatus.Same, byShape.Entries.Single(entry => entry.Key == SyncItemKey.ForCommand(CommandNamed(mine.Mapping, AppGroup.GlobalName, "Minimize").Id)).Status);
        Assert.DoesNotContain(byShape.Preview.Gestures, gesture => gesture.Name == "North");
        Assert.Equal(ImportStatus.New, byName.Entries.Single(entry => entry.Name == "North").Status);
        Assert.Contains(byName.Conflicts, conflict => conflict.Name == "Minimize");
    }

    [Fact]
    public void AHoldRemapRenamedThere_IsMatchedByItsHoldKey_AndItsCommandsByName()
    {
        var mine = SampleConfig();
        var theirs = WithGroup(SampleConfig(), "Blender", blender => blender with { HoldRemaps = [blender.HoldRemaps[0] with { Name = "Navigate" }] });

        var plan = ImportPlan.Create(Exported(theirs, ExportScope.Of([GroupNamed(theirs.Mapping, "Blender").Id])), mine);

        var conflict = Assert.Single(plan.Conflicts);
        Assert.Equal((SyncItemKind.HoldRemap, "Space"), (conflict.Kind, conflict.Name));
        Assert.Equal(new ImportMatch(ImportMatchKind.HoldKey, "Navigate"), plan.Entries.Single(entry => entry.Kind == SyncItemKind.HoldRemap).Match);
        Assert.All(plan.Entries.Where(entry => entry.Kind == SyncItemKind.Command), entry => Assert.Equal(ImportStatus.Same, entry.Status));
        Assert.Equal("Navigate", GroupNamed(plan.Resolve(new ImportChoices { Default = SyncChoice.TakeTheirs }).Mapping, "Blender").HoldRemaps[0].Name);
    }

    [Fact]
    public void ANewItemWhoseNameIsTaken_IsRenamedWithTheSyncsSuffix()
    {
        var mine = SampleConfig();
        var theirs = WithGroup(mine, "Chrome", chrome => chrome with { Name = "Firefox" });
        theirs = theirs with { Mapping = MappingRules.ValidDocument(theirs.Mapping with { Groups = [.. theirs.Mapping.Groups, NewGroup("Chrome")] }) };

        var result = ImportPlan.Create(Exported(theirs, ExportScope.Everything), mine).Preview;

        Assert.Equal(["Blender", "Chrome", "Chrome (2)"], result.Mapping.Groups.Where(group => !group.IsGlobal).Select(group => group.Name));
        Assert.Equal("Incoming app group 'Chrome' renamed 'Chrome (2)': the name is taken.", Assert.Single(result.Repairs).Description);
    }

    [Fact]
    public void ANewCommandOnAGestureAlreadyBoundInItsGroup_ArrivesUnbound_AndNothingIsDeleted()
    {
        var mine = SampleConfig();
        var down = GestureNamed(mine.Gestures, "Down");
        var theirs = WithGroup(mine, "Chrome", chrome => chrome with { Commands = [NewCommand("Close window", down.Id, NewStep("ctrl+shift+w"))] });

        var result = ImportPlan.Create(Exported(theirs, ExportScope.Of([GroupNamed(mine.Mapping, "Chrome").Id])), mine).Preview;

        Assert.Equal(Trigger.None, CommandNamed(result.Mapping, "Chrome", "Close window").Trigger);
        Assert.Equal(SyncRepairKind.Unbound, Assert.Single(result.Repairs).Kind);
        Assert.Equal(Trigger.ForGesture(down.Id), CommandNamed(result.Mapping, "Chrome", "Close tab").Trigger);
    }

    [Fact]
    public void TheGlobalShellOfASelection_ImportsNothing_NotEvenItsActiveFlag()
    {
        var mine = SampleConfig();
        mine = mine with { Mapping = MappingRules.ValidDocument(mine.Mapping with { Groups = [mine.Mapping.Global with { IsActive = false }, .. mine.Mapping.Groups.Skip(1)] }) };
        var theirs = SampleConfig();

        var shell = ImportPlan.Create(Exported(theirs, ExportScope.Of([GroupNamed(theirs.Mapping, "Chrome").Id])), mine);
        var global = ImportPlan.Create(Exported(theirs, ExportScope.Of([GroupId.Global])), mine);

        Assert.DoesNotContain(shell.Entries, entry => entry.Key == SyncItemKey.ForGroup(GroupId.Global));
        Assert.True(shell.IsEmpty);
        Assert.Equal(SyncItemKey.ForGroup(GroupId.Global), Assert.Single(global.Conflicts).Key);
    }

    private static ImportChoices Choose(SyncItemKey key, SyncChoice choice) => new() { Items = new Dictionary<SyncItemKey, SyncChoice> { [key] = choice } };

    /// <summary>Mine, and a file of Chrome whose Close tab sends Ctrl+F4 instead.</summary>
    private static (ConfigDocument Mine, TransferFile File) CloseTabChangedThere()
    {
        var mine = SampleConfig();
        var theirs = WithGroup(mine, "Chrome", chrome => chrome with { Commands = [.. chrome.Commands.Select(command => command with { Steps = [NewStep("ctrl+f4")] })] });
        return (mine, Exported(theirs, ExportScope.Of([GroupNamed(mine.Mapping, "Chrome").Id])));
    }

    /// <summary>Mine, and a file of Chrome where Up (Global's Minimize uses it here) was redrawn up-right and a new Reload uses it.</summary>
    private static (ConfigDocument Mine, TransferFile File) UpRedrawnThereAndUsedByANewCommand()
    {
        var mine = SampleConfig();
        var up = GestureNamed(mine.Gestures, "Up");
        var theirs = mine with { Gestures = [.. mine.Gestures.Select(gesture => gesture.Id == up.Id ? gesture with { Samples = [new GestureSample(StockFlicks.Template("UpRight"))] } : gesture)] };
        theirs = WithGroup(theirs, "Chrome", chrome => chrome with { Commands = [.. chrome.Commands, NewCommand("Reload", up.Id, NewStep("f5")).WithTriggerFor(HostPlatform.MacOS, Trigger.ForGesture(up.Id), DateTimeOffset.UnixEpoch)] });
        return (mine, Exported(theirs, ExportScope.Of([GroupNamed(mine.Mapping, "Chrome").Id])));
    }
}
