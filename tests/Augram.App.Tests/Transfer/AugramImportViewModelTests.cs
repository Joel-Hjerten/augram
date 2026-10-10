using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.Core.Capture;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Sync;
using Augram.Core.Transfer;
using Xunit;
using static Augram.App.Tests.Transfer.TransferTestData;

namespace Augram.App.Tests.Transfer;

/// <summary>
/// The import review over an in-memory session (plan 0003 step 4): the "Done when" group arriving whole, a file already here,
/// the differing items and their choices, apply to all, the options toggle, matching lines and a repair, one undo step per
/// store, and the stores moving while the review is open.
/// </summary>
public sealed class AugramImportViewModelTests
{
    private readonly ListEventLog _log = new();

    [Fact]
    public void AGroupExportedFromOneConfiguration_ArrivesWholeInOneThatLacksIt()
    {
        var source = Configuration();
        var blender = GroupNamed(source.Mapping, "Blender");
        using var session = Session(WithoutGroup(Configuration(), "Blender"));
        var vm = Review(session, Exporter.Export(ExportScope.Of([blender.Id]), source), "Blender.augram.json");

        Assert.Equal("1 gesture, 1 app group, 1 hold remap, 2 commands", vm.ContentsText);
        Assert.Equal("New: 1 app group, 1 hold remap, 2 commands · Same as yours: 1 gesture", vm.CountsText);
        Assert.False(vm.HasConflicts);
        Assert.False(vm.HasSettings);
        Assert.True(vm.CanImport);
        var finished = 0;
        vm.Finished += (_, _) => finished++;

        Assert.True(vm.Import());

        var arrived = GroupNamed(session.Mapping.Current, "Blender");
        Assert.Equal(blender.Matcher!.WindowsProcessNames, arrived.Matcher!.WindowsProcessNames);
        Assert.Equal(PlatformSet.Windows, arrived.UseOn);
        Assert.Equal("Space", Assert.Single(arrived.HoldRemaps).Name);
        Assert.Equal(["Orbit", "Undo"], arrived.Commands.Select(command => command.Name).Order(StringComparer.Ordinal));
        Assert.Equal(arrived.HoldRemaps[0].Id, CommandNamed(session.Mapping.Current, "Blender", "Orbit").HoldRemapId);
        Assert.Equal("Imported from Blender.augram.json: 1 app group, 1 hold remap and 2 commands added.", vm.ResultText);
        Assert.Empty(vm.ResultDetails);
        Assert.Equal(1, finished);
        Assert.False(vm.CanImport);

        // One undo step on the mapping store, none on the others (nothing changed there).
        Assert.False(session.Gestures.CanUndo);
        Assert.False(session.Settings.CanUndo);
        Assert.True(session.Mapping.Undo());
        Assert.DoesNotContain(session.Mapping.Current.Groups, group => group.Name == "Blender");
        Assert.False(session.Mapping.CanUndo);

        var logged = Assert.Single(_log.Events, e => e.Source == AugramImportViewModel.LogSource);
        Assert.Equal((EventLevel.Info, AugramImportViewModel.LogMessage), (logged.Level, logged.Message));
        Assert.Contains(logged.Properties!, property => property.Key == "counts" && Equals(property.Value, "groups +1 ~0 -0, commands +2 ~0 -0, hold remaps +1 ~0 -0"));
    }

    [Fact]
    public void AFileOfWhatIsAlreadyHere_HasNothingToImport()
    {
        var mine = Configuration();
        using var session = Session(mine);
        var vm = Review(session, Exporter.Export(ExportScope.Everything, mine));

        Assert.True(vm.IsEmpty);
        Assert.False(vm.CanImport);
        Assert.StartsWith("Same as yours: ", vm.CountsText, StringComparison.Ordinal);
        Assert.False(vm.Import());
        Assert.Null(vm.ResultText);
        Assert.False(session.Mapping.CanUndo || session.Gestures.CanUndo || session.Settings.CanUndo);
    }

    [Fact]
    public void DifferingItemsAreRows_KeepMineByDefault_AndEachStoreThatChangesIsOneUndoStep()
    {
        var mine = Configuration();
        var theirs = WithGroup(Redrawn(mine, "Z", "S"), "Chrome", group => group with
        {
            Commands = [.. group.Commands.Select(command => command.Name == "Close tab" ? command with { Steps = [new CommandStep(new DelayStep(99), Core.Abstractions.HostPlatform.Windows)] } : command)],
        });
        theirs = theirs with { Mapping = MappingRules.ValidDocument(theirs.Mapping with { Groups = [.. theirs.Mapping.Groups, Firefox()] }) };
        using var session = Session(mine);
        var vm = Review(session, Exporter.Export(ExportScope.Everything, theirs));

        Assert.Equal(["Z", "Close tab"], vm.ConflictEntries.Select(entry => entry.Name));
        Assert.Equal("Gesture · with theirs.augram.json", vm.ConflictEntries[0].Detail);
        Assert.True(vm.ConflictEntries[0].Mine.HasGlyph && vm.ConflictEntries[0].Theirs.HasGlyph);
        Assert.Contains("Wait 99 ms", vm.ConflictEntries[1].Theirs.Text, StringComparison.Ordinal);
        Assert.All(vm.ConflictEntries, entry => Assert.Equal(SyncChoice.KeepMine, entry.Selected));
        Assert.Contains("Different: 1 gesture, 1 command", vm.CountsText, StringComparison.Ordinal);
        Assert.Equal((0, 0), (vm.Preview.Counts.Gestures.Changed, vm.Preview.Counts.Commands.Changed));

        Assert.True(vm.Choose(0, SyncChoice.TakeTheirs));
        Assert.True(vm.Choose(1, SyncChoice.TakeTheirs));
        Assert.Equal((1, 1), (vm.Preview.Counts.Gestures.Changed, vm.Preview.Counts.Commands.Changed));
        Assert.True(vm.Import());

        Assert.Equal(theirs.Gestures.Single(gesture => gesture.Name == "Z").Samples, session.Gestures.All.Single(gesture => gesture.Name == "Z").Samples);
        Assert.Equal(99, ((DelayStep)CommandNamed(session.Mapping.Current, "Chrome", "Close tab").Steps[0].Step).Milliseconds);
        Assert.Contains(session.Mapping.Current.Groups, group => group.Name == "Firefox");
        Assert.Equal("Imported from theirs.augram.json: 1 app group and 1 command added; 1 gesture and 1 command changed.", vm.ResultText);

        Assert.True(session.Gestures.Undo());
        Assert.False(session.Gestures.CanUndo);
        Assert.Equal(mine.Gestures.Single(gesture => gesture.Name == "Z").Samples, session.Gestures.All.Single(gesture => gesture.Name == "Z").Samples);
        Assert.True(session.Mapping.Undo());
        Assert.False(session.Mapping.CanUndo);
        Assert.DoesNotContain(session.Mapping.Current.Groups, group => group.Name == "Firefox");
        Assert.Equal(30, ((DelayStep)CommandNamed(session.Mapping.Current, "Chrome", "Close tab").Steps[0].Step).Milliseconds);
    }

    [Fact]
    public void ApplyToAll_SetsEveryRowThatOffersTheChoice_AndThePreviewSaysWhatTheResultNeeds()
    {
        var mine = Configuration();
        var theirs = WithGroup(Redrawn(mine, "Z", "S"), "Chrome", group => group with
        {
            Matcher = new AppMatcher { WindowsProcessNames = ["chrome.exe", "chromium.exe"] },
            Commands = [.. group.Commands.Select(command => command.Name == "Close tab" ? command with { IsActive = false } : command)],
        });
        using var session = Session(mine);
        var vm = Review(session, Exporter.Export(ExportScope.Everything, theirs));
        var before = vm.ConflictEntries;
        Assert.Equal(["Z", "Chrome", "Close tab"], before.Select(entry => entry.Name));
        Assert.False(vm.HasOutcome);

        vm.ApplyToAll(SyncChoice.KeepBoth);

        Assert.NotSame(before, vm.ConflictEntries);
        Assert.Equal([SyncChoice.KeepBoth, SyncChoice.KeepMine, SyncChoice.KeepBoth], vm.ConflictEntries.Select(entry => entry.Selected));
        Assert.True(vm.HasOutcome);
        Assert.Contains("'Z (2)'", vm.OutcomeText, StringComparison.Ordinal);
        Assert.Contains("'Close tab (2)'", vm.OutcomeText, StringComparison.Ordinal);

        vm.ApplyToAll(SyncChoice.TakeTheirs);
        Assert.All(vm.ConflictEntries, entry => Assert.Equal(SyncChoice.TakeTheirs, entry.Selected));
        Assert.False(vm.HasOutcome);
        Assert.Equal(1, vm.Preview.Counts.Groups.Changed);
    }

    [Fact]
    public void TheOptionsToggle_IsOfferedOnlyWhenTheFileHasOptions_AndIsOffUntilTicked()
    {
        var mine = Configuration();
        var theirs = mine with { Settings = mine.Settings with { General = mine.Settings.General with { StrokeButton = MouseButton.Middle } } };
        using var session = Session(mine);
        var vm = Review(session, Exporter.Export(ExportScope.Everything, theirs));

        Assert.True(vm.HasSettings);
        Assert.False(vm.TakeSettings);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.Preview.SettingsChanged);

        vm.TakeSettings = true;
        Assert.True(vm.Preview.SettingsChanged);
        Assert.True(vm.Import());

        Assert.Equal(MouseButton.Middle, session.Settings.Current.General.StrokeButton);
        Assert.Equal(mine.Settings.Sync, session.Settings.Current.Sync);
        Assert.Equal("Imported from theirs.augram.json: options taken.", vm.ResultText);
        Assert.True(session.Settings.CanUndo);

        using var other = Session(Configuration());
        Assert.False(Review(other, Exporter.Export(ExportScope.GesturesOnly, theirs)).HasSettings);
    }

    [Fact]
    public void ItemsWithUnknownIdsAreMatchedByNameHoldKeyAndShape_AndListedOnePerLine()
    {
        // Another machine that never shared ids with this one, where Up is drawn the same but called North.
        var theirs = Configuration();
        var north = theirs.Gestures.Single(gesture => gesture.Name == "Up") with { Id = GestureId.New(), Name = "North" };
        theirs = WithGroup(theirs with { Gestures = [.. theirs.Gestures.Where(gesture => gesture.Name != "Up"), north] }, "Chrome", group => group with
        {
            Commands = [.. group.Commands.Select(command => command.Name == "Close tab" ? command with { Trigger = Trigger.ForGesture(north.Id) } : command)],
        });
        using var session = Session(Configuration());
        var vm = Review(session, Exporter.Export(ExportScope.Everything, theirs));

        Assert.True(vm.HasMatches);
        var lines = vm.MatchesText.Split(Environment.NewLine);
        Assert.Contains("App group 'Chrome' in the file is your 'Chrome'.", lines);
        Assert.Contains("Command 'Orbit' in the file is your 'Orbit'.", lines);
        Assert.Contains("Hold remap 'Space' in the file is your 'Space': the same hold key.", lines);
        Assert.Contains("Excluded app 'Game' in the file is your 'Game'.", lines);
        Assert.Contains(lines, line => line.StartsWith("Gesture 'North' in the file has the shape of your 'Up' (", StringComparison.Ordinal));
        Assert.Equal(["Up"], vm.ConflictEntries.Select(entry => entry.Name));
    }

    [Fact]
    public void MoreMatchesThanTheReviewListsEndWithACount()
    {
        var entries = Enumerable.Range(1, AugramImportViewModel.MatchLinesShown + 2)
            .Select(i => new ImportEntry(SyncItemKey.ForGesture(GestureId.New()), $"G{i}", ImportStatus.Same) { Match = new ImportMatch(ImportMatchKind.Name, $"G{i}") })
            .ToList();

        var lines = AugramImportViewModel.Matches(entries).Split(Environment.NewLine);

        Assert.Equal(AugramImportViewModel.MatchLinesShown + 1, lines.Length);
        Assert.Equal("…and 2 more.", lines[^1]);
    }

    [Fact]
    public void AnIncomingTriggerAlreadyUsedHereIsUnbound_UnlessTheChoicesFreeIt()
    {
        var mine = Configuration();
        var theirs = WithGroup(mine, "Chrome", group => group with
        {
            Commands =
            [
                .. group.Commands.Select(command => command.Name == "Close tab" ? command with { Trigger = Trigger.ForGesture(Down) } : command),
                Cmd("Back", Trigger.ForGesture(Up), new DelayStep(5)),
            ],
        });
        using var session = Session(mine);
        var vm = Review(session, Exporter.Export(ExportScope.Everything, theirs));

        Assert.True(vm.HasOutcome);
        Assert.Contains("'Back'", vm.OutcomeText, StringComparison.Ordinal);
        Assert.Contains(vm.Preview.Repairs, repair => repair.Kind == SyncRepairKind.Unbound);

        Assert.True(vm.Choose(0, SyncChoice.TakeTheirs));
        Assert.False(vm.HasOutcome);
        Assert.True(vm.Import());
        Assert.Equal(Trigger.ForGesture(Up), CommandNamed(session.Mapping.Current, "Chrome", "Back").Trigger);
    }

    [Fact]
    public void WhenTheStoresMoveWhileTheReviewIsOpen_TheImportPlansAgainWithTheSameChoices()
    {
        var source = Configuration();
        using var session = Session(WithoutGroup(Configuration(), "Blender"));
        var vm = Review(session, Exporter.Export(ExportScope.Of([GroupNamed(source.Mapping, "Blender").Id]), source), "Blender.augram.json");

        // A sync (or any edit) lands after the review was planned: the reviewed plan is refused, the import plans again.
        session.Mapping.AddGroup(new AppGroup(GroupId.New(), "Notepad", IsActive: true, SuppressGlobals: false, Matcher: null, []));

        Assert.True(vm.Import());

        Assert.Contains(session.Mapping.Current.Groups, group => group.Name == "Notepad");
        Assert.Contains(session.Mapping.Current.Groups, group => group.Name == "Blender");
        var logged = Assert.Single(_log.Events, e => e.Source == AugramImportViewModel.LogSource);
        Assert.Contains(logged.Properties!, property => property.Key == "replanned" && Equals(property.Value, 1));
    }

    [Fact]
    public void CancelFinishesAndChangesNothing()
    {
        var source = Configuration();
        using var session = Session(WithoutGroup(Configuration(), "Blender"));
        var vm = Review(session, Exporter.Export(ExportScope.Of([GroupNamed(source.Mapping, "Blender").Id]), source));
        var finished = 0;
        vm.Finished += (_, _) => finished++;

        vm.Cancel();

        Assert.Equal(1, finished);
        Assert.Null(vm.ResultText);
        Assert.False(session.Mapping.CanUndo);
        Assert.DoesNotContain(_log.Events, e => e.Source == AugramImportViewModel.LogSource);
    }

    private AugramImportViewModel Review(ConfigSession session, TransferFile file, string name = "theirs.augram.json")
        => new(session, Read(file), name, _log);

    /// <summary>Written and read back, as a file on disk would be.</summary>
    private static TransferFile Read(TransferFile file) => TransferSerializer.Read(TransferSerializer.Write(file), Core.Steps.StepRegistry.BuiltIn);

    private static AppGroup Firefox()
        => new(GroupId.New(), "Firefox", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["firefox.exe"] }, [Cmd("Reload", Trigger.ForGesture(Up), new DelayStep(20))]);
}
