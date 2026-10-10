using Augram.App.Components.GestureGlyph;
using Augram.App.Components.SyncConflictList;
using Augram.App.Hosting;
using Augram.App.Sync;
using Augram.App.Tests.Support;
using Augram.App.Tests.Sync.Support;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Sync;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary>The conflict dialog: one entry per conflict with both versions (glyphs for gestures), the choices each kind allows, and the resolutions Apply hands over.</summary>
public sealed class SyncConflictsTests
{
    private static readonly Gesture Zig = SyncMachine.NewGesture("Zig", 100, 100);
    private static readonly AppGroup Chrome = MappingFixture.Group("Chrome");
    private static readonly Command CloseTab = MappingFixture.Command("Close tab", Zig.Id);

    [Fact]
    public void KeepBothIsOfferedOnlyForAGestureOrCommandPresentOnBothMachines()
    {
        Assert.Equal([SyncChoice.KeepMine, SyncChoice.TakeTheirs, SyncChoice.KeepBoth], SyncConflictsViewModel.Allowed(GestureConflict()));
        Assert.Equal([SyncChoice.KeepMine, SyncChoice.TakeTheirs, SyncChoice.KeepBoth], SyncConflictsViewModel.Allowed(CommandConflict()));
        Assert.Equal([SyncChoice.KeepMine, SyncChoice.TakeTheirs], SyncConflictsViewModel.Allowed(GroupConflict()));
        Assert.Equal([SyncChoice.KeepMine, SyncChoice.TakeTheirs], SyncConflictsViewModel.Allowed(GestureConflict() with { RemoteContent = null }));
        Assert.Equal([SyncChoice.KeepMine, SyncChoice.TakeTheirs], SyncConflictsViewModel.Allowed(CommandConflict() with { LocalContent = null }));
    }

    [Fact]
    public void EntriesShowBothVersions()
    {
        var vm = ViewModel(GestureConflict(), CommandConflict(), GroupConflict(), GestureConflict() with { RemoteContent = null });

        var gesture = vm.Entries[0];
        Assert.Equal("Zig", gesture.Name);
        Assert.Equal("Gesture · with Mac", gesture.Detail);
        Assert.True(gesture.Mine.HasGlyph);
        Assert.True(gesture.Theirs.HasGlyph);
        Assert.Equal(new GesturePoint(100, 100), gesture.Mine.Points![^1]);
        Assert.Equal(new GesturePoint(-100, 100), gesture.Theirs.Points![^1]);
        Assert.Equal([SyncConflictsViewModel.KeepMineLabel, SyncConflictsViewModel.TakeTheirsLabel, SyncConflictsViewModel.KeepBothLabel], gesture.Choices.Select(choice => choice.Label));
        Assert.Equal(SyncChoice.KeepMine, gesture.Selected);

        var command = vm.Entries[1];
        Assert.False(command.Mine.HasGlyph);
        Assert.Equal("Chrome › Close tab: gesture Zig → Wait 30 ms", command.Mine.Text);
        Assert.Contains("Wait 120 ms", command.Theirs.Text, StringComparison.Ordinal);

        Assert.Equal("Chrome: matches no app yet", vm.Entries[2].Mine.Text);
        Assert.Equal("Google Chrome: matches no app yet, suppresses global commands", vm.Entries[2].Theirs.Text);
        Assert.Equal(SyncConflictSide.DeletedText, vm.Entries[3].Theirs.Text);
        Assert.False(vm.Entries[3].Theirs.HasGlyph);
        Assert.Equal("4 conflicts with Mac", vm.Summary);
    }

    /// <summary>Plan 0002 step 2's sync item kind, labelled in the dialog (step 4): its header's name, hold key, tap time, Use on and active.</summary>
    [Fact]
    public void AHoldRemapConflictIsLabelledAndShowsItsHeaderOnBothSides()
    {
        var space = HoldRemap.For(KeyCode.Space);
        var conflict = Conflict(
            new SyncItem.HoldRemapItem(Chrome.Id, space),
            new SyncItem.HoldRemapItem(Chrome.Id, space with { TapTimeMs = 220, UseOn = PlatformSet.Windows, IsActive = false }));

        var entry = ViewModel(conflict).Entries.Single();

        Assert.Equal("Hold remap", SyncConflictsViewModel.KindLabel(SyncItemKind.HoldRemap));
        Assert.Equal(("Space", "Hold remap · with Mac"), (entry.Name, entry.Detail));
        Assert.Equal("Space: hold Space, tap 180 ms", entry.Mine.Text);
        Assert.Equal("Space: hold Space, tap 220 ms · Windows only (inactive)", entry.Theirs.Text);
        Assert.Equal([SyncChoice.KeepMine, SyncChoice.TakeTheirs], SyncConflictsViewModel.Allowed(conflict));
    }

    /// <summary>A command under a hold remap reads "group › hold remap › command" (Joel, 2026-10-10: names are unique per parent only).</summary>
    [Fact]
    public void ACommandUnderAHoldRemapIsLabelledWithItsHoldRemap()
    {
        var space = HoldRemap.For(KeyCode.Space);
        var orbit = CloseTab with { Id = CommandId.New(), Name = "Orbit", Trigger = Trigger.ForInput(HoldInput.Of(Core.Capture.MouseButton.Left)), HoldRemapId = space.Id };
        var elsewhere = orbit with { Id = CommandId.New(), HoldRemapId = HoldRemapId.New() };
        var mapping = new MappingDocument([AppGroup.EmptyGlobal, Chrome with { HoldRemaps = [space], Commands = [orbit] }], []);

        var entries = new SyncConflictsViewModel(
            [Conflict(new SyncItem.CommandItem(Chrome.Id, orbit), new SyncItem.CommandItem(Chrome.Id, orbit with { IsActive = false })), Conflict(new SyncItem.CommandItem(Chrome.Id, elsewhere), new SyncItem.CommandItem(Chrome.Id, elsewhere))],
            [Zig],
            mapping).Entries;

        Assert.Equal("Chrome › Space › Orbit: Left → Wait 30 ms", entries[0].Mine.Text);
        Assert.Equal("Chrome › Space › Orbit: Left → Wait 30 ms (inactive)", entries[0].Theirs.Text);
        Assert.Equal("Chrome › another hold remap › Orbit: Left → Wait 30 ms", entries[1].Mine.Text);
    }

    [Fact]
    public void ChoicesBecomeResolutionsInOrderAndAChoiceNotOfferedIsRefused()
    {
        var gesture = GestureConflict();
        var group = GroupConflict();
        var vm = ViewModel(gesture, group);

        Assert.True(vm.Choose(0, SyncChoice.KeepBoth));
        Assert.False(vm.Choose(1, SyncChoice.KeepBoth));
        Assert.True(vm.Choose(1, SyncChoice.TakeTheirs));

        Assert.Equal([new SyncResolution(gesture, SyncChoice.KeepBoth), new SyncResolution(group, SyncChoice.TakeTheirs)], vm.Resolutions());
    }

    [AvaloniaFact]
    public void TheListShowsOneRowPerConflictWithGlyphsForGesturesAndReportsChoices()
    {
        var vm = ViewModel(GestureConflict(), GroupConflict());
        var list = new SyncConflictList { Entries = vm.Entries };
        list.ChoiceChanged += (_, e) => vm.Choose(e.Index, e.Choice);
        new Window { Content = list, Width = 900, Height = 400 }.Show();

        var rows = list.GetVisualDescendants().OfType<SyncConflictRow>().ToList();
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].HasMineGlyph && rows[0].HasTheirsGlyph);
        Assert.False(rows[1].HasMineGlyph || rows[1].HasTheirsGlyph);
        Assert.Equal(2, rows[0].GetVisualDescendants().OfType<GestureGlyph>().Count(glyph => glyph.IsVisible && glyph.Classes.Contains("row-glyph")));
        Assert.Equal([SyncConflictsViewModel.KeepMineLabel, SyncConflictsViewModel.TakeTheirsLabel], rows[1].ChoiceLabels);

        var combo = rows[1].GetVisualDescendants().OfType<ComboBox>().Single();
        Assert.Equal(0, combo.SelectedIndex);
        combo.SelectedIndex = 1;
        Assert.Equal(SyncChoice.TakeTheirs, vm.Choices[1]);
        Assert.False(rows[1].Choose(SyncChoice.KeepBoth));
        Assert.Equal(1, combo.SelectedIndex);
        Assert.True(rows[0].Choose(SyncChoice.KeepBoth));
        Assert.Equal(SyncChoice.KeepBoth, vm.Choices[0]);
    }

    [AvaloniaFact]
    public void TheWindowAppliesOnlyWhenApplyIsPressed()
    {
        var vm = ViewModel(GestureConflict());
        var window = new SyncConflictWindow(vm);
        window.Show();
        Assert.Equal("1 conflict with Mac", window.GetVisualDescendants().OfType<TextBlock>().First(text => text.Classes.Contains("section-title")).Text);

        window.Close();
        Assert.False(window.Applied);

        var applied = new SyncConflictWindow(vm);
        applied.Show();
        applied.Apply();
        Assert.True(applied.Applied);
    }

    private static SyncConflictsViewModel ViewModel(params SyncConflict[] conflicts)
        => new(conflicts, [Zig], new MappingDocument([AppGroup.EmptyGlobal, Chrome with { Commands = [CloseTab] }], []));

    private static SyncConflict GestureConflict()
        => Conflict(new SyncItem.GestureItem(Zig), new SyncItem.GestureItem(Zig with { Samples = SyncMachine.NewGesture("x", -100, 100).Samples }));

    private static SyncConflict CommandConflict()
        => Conflict(
            new SyncItem.CommandItem(Chrome.Id, CloseTab),
            new SyncItem.CommandItem(Chrome.Id, CloseTab with { Steps = [new CommandStep(new Core.Steps.Delay.DelayStep(120), Core.Abstractions.HostPlatform.Windows)] }));

    private static SyncConflict GroupConflict()
        => Conflict(new SyncItem.GroupItem(Chrome), new SyncItem.GroupItem(Chrome with { Name = "Google Chrome", SuppressGlobals = true }));

    private static SyncConflict Conflict(SyncItem mine, SyncItem theirs)
        => new(mine.Key, mine.Name, mine.Content, theirs.Content) { MachineId = Guid.NewGuid(), MachineName = "Mac" };
}
