using Augram.App.Components.CommandTree;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;
using static Augram.App.Tests.Commands.HoldRemapTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// Hold remaps in the Apps tab's tree (F9, plan 0002 step 4): a section nested in its app group's, with its commands under it;
/// New hold remap on a group, rename, the active box, delete (asks; one undo step brings it back with its commands), new
/// command and paste under it, copy and paste of the whole hold remap.
/// </summary>
public sealed class CommandsViewModelHoldRemapTests
{
    [AvaloniaFact]
    public void AnAppGroupShowsItsHoldRemapNestedRightAfterItWithTheCommandsUnderIt()
    {
        var (vm, store, _) = CreateBlender();

        Assert.Equal(["Blender", "Space", "Chrome"], Names(vm));
        var blender = Section(vm, "Blender");
        Assert.Equal(["Undo"], blender.Commands.Select(command => command.Name));
        Assert.True(blender is { CanAddHoldRemap: true, HoldRemapCount: 1, IsNested: false });
        Assert.Equal("1 command · 1 hold remap", blender.CountText);
        Assert.Equal("1 command", Section(vm, "Chrome").CountText);

        var space = HoldRemapSection(vm, "Space");
        Assert.Equal(SectionId.ForHoldRemap(Blender(store).Id, Space(store).Id), space.Id);
        Assert.Equal(blender.Id, space.Id.Parent);
        Assert.Equal("hold remap · 4 commands", space.CountText);
        Assert.True(space is { CanRename: true, CanDelete: true, CanToggleActive: true, CanCopy: true, CanEditDefinition: false, CanAddHoldRemap: false });
        Assert.Equal(["Grab", "Orbit", "Pan", "Zoom both"], space.Commands.Select(command => command.Name));
        Assert.Equal(["W", "Left", "Right", "Left + Right"], space.Commands.Select(command => command.TriggerText));
        Assert.Equal(["Remap to G", "Remap to Middle", "Remap to Shift + Middle", "Remap to Ctrl + Middle"], space.Commands.Select(command => command.StepSummary));
        Assert.All(space.Commands, command =>
        {
            Assert.True(command.IsUnderHoldRemap);
            Assert.Equal(space.Id, command.Section);
            Assert.Empty(command.Categories);
            Assert.Equal("Fires while Space is held.", command.TriggerHint);
        });
    }

    [AvaloniaFact]
    public void NewHoldRemapOnAGroupAddsOneWithoutAKeyAndSelectsItsForm_NotOnGlobal()
    {
        var (vm, store, _) = CreateBlender();

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewHoldRemap, Section(vm, "Chrome")));

        var added = Assert.Single(Group(store, "Chrome").HoldRemaps);
        Assert.Equal(("Hold remap", KeyCode.None, HoldRemap.DefaultTapTimeMs), (added.Name, added.HoldKey, added.TapTimeMs));
        Assert.Equal(SectionId.ForHoldRemap(Group(store, "Chrome").Id, added.Id), vm.SelectedSectionId);
        Assert.Null(vm.SelectedCommandId);
        Assert.True(Section(vm, "Chrome").IsExpanded);
        Assert.True(HoldRemapSection(vm, "Hold remap").IsExpanded);
        Assert.Equal(HoldRemapEditViewModel.Title, vm.GroupForm!.Sections[0].Title);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewHoldRemap, Section(vm, "Chrome")));
        Assert.Equal(["Hold remap", "Hold remap 1"], Group(store, "Chrome").HoldRemaps.Select(holdRemap => holdRemap.Name));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Empty(Group(store, "Chrome").HoldRemaps);

        var (global, _, _) = CreateBlender(CommandsScope.Global);
        global.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewHoldRemap, global.Sections[0]));
        Assert.Equal("A hold remap belongs to an app group: right-click a group on the Apps tab.", global.Message);
        Assert.False(CommandTreeMenu.Allows(CommandTreeAction.NewHoldRemap, global.Sections[0], null));
        Assert.True(CommandTreeMenu.Allows(CommandTreeAction.NewHoldRemap, Section(vm, "Chrome"), null));
        Assert.False(CommandTreeMenu.Allows(CommandTreeAction.NewHoldRemap, HoldRemapSection(vm, "Space"), null));
    }

    [AvaloniaFact]
    public void RenameAndTheActiveBoxActOnTheHoldRemap()
    {
        var (vm, store, _) = CreateBlender();

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, HoldRemapSection(vm, "Space"), null, "Navigate"));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleActive, HoldRemapSection(vm, "Navigate")));

        var stored = Assert.Single(Blender(store).HoldRemaps);
        Assert.Equal(("Navigate", KeyCode.Space, false), (stored.Name, stored.HoldKey, stored.IsActive));
        Assert.False(HoldRemapSection(vm, "Navigate").IsActive);
        Assert.True(Section(vm, "Blender").IsActive);
    }

    [AvaloniaFact]
    public void DeleteAsksThenRemovesTheHoldRemapWithItsCommands_OneUndoBringsAllBack()
    {
        var (vm, store, confirm) = CreateBlender();
        var before = Blender(store);
        confirm.Answer = false;

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, HoldRemapSection(vm, "Space")));

        Assert.Equal(("Delete hold remap", "Delete hold remap 'Space' and its 4 commands?", "Delete"), confirm.Requests[^1]);
        Assert.Single(Blender(store).HoldRemaps);

        confirm.Answer = true;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, HoldRemapSection(vm, "Space")));

        Assert.Empty(Blender(store).HoldRemaps);
        Assert.Equal(["Undo"], Blender(store).Commands.Select(command => command.Name));
        Assert.Equal(["Blender", "Chrome"], Names(vm));
        Assert.Equal($"Deleted 'Space'. {CommandsKeymap.Current.Undo} undoes it.", vm.Message);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));

        Assert.Equal(before, Blender(store));
        Assert.Equal(["Blender", "Space", "Chrome"], Names(vm));
        Assert.Equal(4, HoldRemapSection(vm, "Space").Commands.Count);
    }

    [AvaloniaFact]
    public void DeletingTheGroupCountsTheCommandsUnderItsHoldRemaps()
    {
        var (vm, _, confirm) = CreateBlender();
        confirm.Answer = false;

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Blender")));

        Assert.Equal("Delete group 'Blender' and its 5 commands?", confirm.Requests[^1].Message);
    }

    [AvaloniaFact]
    public void NewCommandUnderAHoldRemapIsUnderItWithNoInputYet()
    {
        var (vm, store, _) = CreateBlender();
        var space = HoldRemapSection(vm, "Space");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, space));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand));

        var created = Find(store, "New command 1");
        Assert.Equal(Space(store).Id, created.HoldRemapId);
        Assert.Same(Trigger.None, created.Trigger);
        Assert.Equal(space.Id, vm.SelectedSectionId);
        Assert.Equal(created.Id, vm.SelectedCommandId);
        Assert.True(vm.SelectedCommand!.IsUnderHoldRemap);
        Assert.Equal(InputKindExtensions.NoInputText, vm.SelectedCommand.TriggerText);
        Assert.Contains("New command 1", HoldRemapSection(vm, "Space").Commands.Select(command => command.Name));
    }

    [AvaloniaFact]
    public void PasteKeepsACommandUnderTheHoldRemap_ElsewhereItLosesItsInput()
    {
        var (vm, store, _) = CreateBlender();
        var space = HoldRemapSection(vm, "Space");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, space, Item(vm, "Grab")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, space));

        var copy = Find(store, "Grab (2)");
        Assert.Equal(Space(store).Id, copy.HoldRemapId);
        Assert.Same(Trigger.None, copy.Trigger);
        Assert.StartsWith("Pasted 'Grab (2)' under 'Space' without its trigger: ", vm.Message, StringComparison.Ordinal);
        Assert.Equal(space.Id, vm.SelectedSectionId);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, Section(vm, "Chrome"), Item(vm, "Close tab")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, space));
        Assert.Equal(Space(store).Id, Blender(store).Commands.Single(command => command.Name == "Close tab").HoldRemapId);
        Assert.Contains("its trigger must be an input", vm.Message, StringComparison.Ordinal);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, space, Item(vm, "Orbit")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, Section(vm, "Chrome")));
        var orbit = Group(store, "Chrome").Commands.Single(command => command.Name == "Orbit");
        Assert.Null(orbit.HoldRemapId);
        Assert.Same(Trigger.None, orbit.Trigger);
        Assert.Equal("Pasted 'Orbit' without its input: only a command under a hold remap has one.", vm.Message);
    }

    [AvaloniaFact]
    public void CopyAndPasteAHoldRemapTakesItsCommandsInOneUndoStep_WithoutAHoldKeyTheGroupUses()
    {
        var (vm, store, _) = CreateBlender();
        Assert.True(CommandTreeMenu.Allows(CommandTreeAction.Copy, HoldRemapSection(vm, "Space"), null));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, HoldRemapSection(vm, "Space")));
        Assert.Equal($"Copied 'Space' with its 4 commands. Paste it into an app group with {CommandsKeymap.Current.Paste}.", vm.Message);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, Section(vm, "Chrome")));

        var chrome = Group(store, "Chrome");
        var pasted = Assert.Single(chrome.HoldRemaps);
        Assert.Equal(("Space", KeyCode.Space), (pasted.Name, pasted.HoldKey));
        Assert.NotEqual(Space(store).Id, pasted.Id);
        Assert.Equal(["Grab", "Orbit", "Pan", "Zoom both"], chrome.Commands.Where(command => command.HoldRemapId == pasted.Id).Select(command => command.Name));
        Assert.Equal(SectionId.ForHoldRemap(chrome.Id, pasted.Id), vm.SelectedSectionId);
        Assert.Equal(["Blender", "Space", "Chrome", "Space"], Names(vm));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Empty(Group(store, "Chrome").HoldRemaps);
        Assert.Equal(["Close tab"], Group(store, "Chrome").Commands.Select(command => command.Name));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, Section(vm, "Blender")));
        var second = Blender(store).HoldRemaps.Single(holdRemap => holdRemap.Name == "Space (2)");
        Assert.Equal(KeyCode.None, second.HoldKey);
        Assert.Equal("Pasted 'Space (2)' into 'Blender' without its hold key: 'Space' in 'Blender' already uses Space as its hold key.", vm.Message);
        Assert.Equal(["Grab", "Orbit", "Pan", "Zoom both"], Blender(store).Commands.Where(command => command.HoldRemapId == second.Id).Select(command => command.Name));
    }

    /// <summary>
    /// Command names are unique within their parent (Joel, 2026-10-10): a pasted Orbit keeps its name under another hold remap
    /// and is "Orbit (2)" under its own; New command counts per parent; a rename is refused only by a sibling.
    /// </summary>
    [AvaloniaFact]
    public void NamesAreFreeWithinTheParent_PasteNewCommandAndRenameAskOnlyTheSiblings()
    {
        var (vm, store, _) = CreateBlender();
        var s = store.AddHoldRemap(Blender(store).Id, HoldRemap.For(KeyCode.S));
        var spaceId = Space(store).Id;

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, HoldRemapSection(vm, "Space"), Item(vm, "Orbit")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, HoldRemapSection(vm, "S")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, HoldRemapSection(vm, "Space")));

        var orbits = Blender(store).Commands.Where(command => command.Name.StartsWith("Orbit", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, orbits.Count);
        Assert.Single(orbits, command => command.Name == "Orbit" && command.HoldRemapId == s.Id && command.Trigger == Trigger.ForInput(HoldInput.Of(MouseButton.Left)));
        Assert.Single(orbits, command => command.Name == "Orbit" && command.HoldRemapId == spaceId);
        Assert.Single(orbits, command => command.Name == "Orbit (2)" && command.HoldRemapId == spaceId);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand, HoldRemapSection(vm, "Space")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand, HoldRemapSection(vm, "S")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand, Section(vm, "Blender")));
        Assert.Equal(3, Blender(store).Commands.Count(command => command.Name == "New command 1"));

        var orbitUnderS = HoldRemapSection(vm, "S").Commands.Single(command => command.Name == "Orbit");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, HoldRemapSection(vm, "S"), orbitUnderS, "Pan"));
        Assert.Equal("Pan", store.FindCommand(orbitUnderS.Id)!.Value.Command.Name);

        var copy = HoldRemapSection(vm, "Space").Commands.Single(command => command.Name == "Orbit (2)");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, HoldRemapSection(vm, "Space"), copy, "Pan"));
        Assert.Equal("A command named 'Pan' already exists under 'Space' in 'Blender'.", vm.Message);
        Assert.Equal("Orbit (2)", store.FindCommand(copy.Id)!.Value.Command.Name);
    }
}
