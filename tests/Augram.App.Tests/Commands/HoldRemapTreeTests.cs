using Augram.App.Components.CommandTree;
using Augram.App.ViewModels.Commands;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;
using static Augram.App.Tests.Commands.HoldRemapTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The tree component with a hold remap (F9, plan 0002 step 4): its header nested in its app group's (shown only while the group
/// is open), its commands one level further in, the menu's New hold remap on a group and Copy on a hold remap, and the keys.
/// </summary>
public sealed class HoldRemapTreeTests
{
    [AvaloniaFact]
    public void AHoldRemapsHeaderSitsInsideItsOpenGroupAndItsCommandsUnderIt()
    {
        var (tree, vm, _) = Show("Blender");

        Assert.Equal(["Blender", "Space", "Chrome"], tree.Rows.OfType<SectionRow>().Select(row => row.NameText));
        Assert.Equal(["Undo"], tree.Rows.OfType<CommandRow>().Select(row => row.NameText));
        var space = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Space");
        Assert.Contains(":nested", space.Classes);
        Assert.DoesNotContain(":nested", tree.Rows.OfType<SectionRow>().First().Classes);
        Assert.Equal("hold remap · 4 commands", space.CountText);
        Assert.True(space.CanToggleActive);
        Assert.True(space.CanRename);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, HoldRemapSection(vm, "Space")));
        tree.Sections = vm.Sections;

        Assert.Equal(["Undo", "Grab", "Orbit", "Pan", "Zoom both"], tree.Rows.OfType<CommandRow>().Select(row => row.NameText));
        var zoom = tree.Rows.OfType<CommandRow>().Single(row => row.NameText == "Zoom both");
        Assert.Contains(":nested", zoom.Classes);
        Assert.Equal(("Left + Right", "Left +\nRight", "Remap to Ctrl + Middle"), (zoom.TriggerText, zoom.BadgeText, zoom.SummaryText));
        Assert.DoesNotContain(":nested", tree.Rows.OfType<CommandRow>().First().Classes);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(vm, "Blender")));
        tree.Sections = vm.Sections;

        Assert.Equal(["Blender", "Chrome"], tree.Rows.OfType<SectionRow>().Select(row => row.NameText));
        Assert.Empty(tree.Rows.OfType<CommandRow>());
    }

    [AvaloniaFact]
    public void TheMenuOffersNewHoldRemapOnAGroup_CopyRenameAndDeleteOnAHoldRemap()
    {
        var (tree, vm, _) = Show("Blender");
        var menu = CommandTreeMenuOf(tree);

        tree.SelectedSectionId = HoldRemapSection(vm, "Space").Id;
        CommandTreeMenu.Refresh(menu, tree.SelectedSection, tree.SelectedCommand, vm.NewSectionLabel);
        Assert.False(Entry(menu, CommandTreeAction.NewHoldRemap).IsVisible);
        Assert.True(Entry(menu, CommandTreeAction.Copy).IsEnabled);
        Assert.True(Entry(menu, CommandTreeAction.Rename).IsEnabled);
        Assert.True(Entry(menu, CommandTreeAction.Delete).IsEnabled);

        tree.SelectedSectionId = Section(vm, "Blender").Id;
        CommandTreeMenu.Refresh(menu, tree.SelectedSection, tree.SelectedCommand, vm.NewSectionLabel);
        Assert.True(Entry(menu, CommandTreeAction.NewHoldRemap).IsVisible);
        Assert.Equal("New hold remap", Entry(menu, CommandTreeAction.NewHoldRemap).Header);
        Assert.False(Entry(menu, CommandTreeAction.Copy).IsEnabled);
    }

    [AvaloniaFact]
    public void TheKeysActOnASelectedHoldRemap()
    {
        var (tree, vm, actions) = Show("Blender");
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        tree.SelectedSectionId = HoldRemapSection(vm, "Space").Id;
        var list = tree.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.True(list.ContainerFromItem(list.SelectedItem!)!.Focus());
        var modifier = CommandsKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;

        window.KeyPressQwerty(PhysicalKey.C, modifier);
        window.KeyPressQwerty(PhysicalKey.N, modifier);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);

        Assert.Equal([CommandTreeAction.Copy, CommandTreeAction.NewCommand, CommandTreeAction.Delete], actions.Select(action => action.Action));
        Assert.All(actions, action =>
        {
            Assert.Equal("Space", action.Section!.Name);
            Assert.Null(action.Command);
        });
    }

    private static ContextMenu CommandTreeMenuOf(CommandTree tree) => tree.GetVisualDescendants().OfType<ListBox>().Single().ContextMenu!;

    private static MenuItem Entry(ContextMenu menu, CommandTreeAction action) => menu.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, action));

    private static (CommandTree Tree, CommandsViewModel Vm, List<CommandTreeActionEventArgs> Actions) Show(string expand)
    {
        var (vm, _, _) = CreateBlender();
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(vm, expand)));
        var actions = new List<CommandTreeActionEventArgs>();
        var tree = new CommandTree { Sections = vm.Sections, Heading = vm.Heading, NewSectionLabel = vm.NewSectionLabel };
        tree.ActionRequested += (_, e) => actions.Add(e);
        new Window { Content = tree, Width = 800, Height = 600 }.Show();
        return (tree, vm, actions);
    }
}
