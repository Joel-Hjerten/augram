using Augram.App.Components.CommandsWorkbench;
using Augram.App.Components.CommandTree;
using Augram.App.Declarations;
using Augram.App.Screens;
using Augram.App.ViewModels.Commands;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;
using static Augram.App.Tests.Commands.HoldRemapTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// New command on the header of the section it targets (Joel, 2026-10-11: "place it over the currently selected group"), through
/// the workbench and the view model as the app wires them: a click adds a command there (a Global category, Uncategorized, an app
/// group, a hold remap) and never folds the section; with nothing selected no header shows it and Ctrl/Cmd+N makes nothing.
/// </summary>
public sealed class SectionNewCommandTests
{
    [AvaloniaFact]
    public void TheButtonAddsACommandToTheSelectedCategoryAndToUncategorized_TwiceStaysOpen()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        var (bench, window) = Show(vm);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Media")));
        Assert.False(Section(vm, "Media").IsExpanded);

        ClickNewCommand(bench, window, "Media");

        var created = Find(store, "New command 1");
        Assert.Equal(Category(store, "Media").Id, created.CategoryId);
        Assert.Equal(created.Id, vm.SelectedCommandId);
        Assert.True(Section(vm, "Media").IsExpanded);
        Assert.Equal(["Media"], ShowingNewCommand(bench));

        // A second click on the same header adds another and leaves the section open: the button never toggles it.
        ClickNewCommand(bench, window, "Media");

        Assert.Equal(Category(store, "Media").Id, Find(store, "New command 2").CategoryId);
        Assert.True(Section(vm, "Media").IsExpanded);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized")));
        ClickNewCommand(bench, window, "Uncategorized");

        Assert.Null(Find(store, "New command 3").CategoryId);
        Assert.Equal(SectionId.Uncategorized, vm.SelectedSectionId);
    }

    [AvaloniaFact]
    public void TheButtonAddsACommandToTheSelectedAppGroupAndUnderTheSelectedHoldRemap()
    {
        var (vm, store, _) = CreateBlender();
        var (bench, window) = Show(vm);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(vm, "Blender")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Blender")));

        ClickNewCommand(bench, window, "Blender");

        var plain = Find(store, "New command 1");
        Assert.Contains(plain, Blender(store).Commands);
        Assert.Null(plain.HoldRemapId);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, HoldRemapSection(vm, "Space")));
        Assert.Equal(["Space"], ShowingNewCommand(bench));
        ClickNewCommand(bench, window, "Space");

        var under = Blender(store).Commands.Single(command => command.Name == "New command 1" && command.HoldRemapId is not null);
        Assert.Equal(Space(store).Id, under.HoldRemapId);
        Assert.True(vm.SelectedCommand!.IsUnderHoldRemap);
        Assert.Equal(HoldRemapSection(vm, "Space").Id, vm.SelectedSectionId);
    }

    [AvaloniaFact]
    public void WithNothingSelectedNoHeaderShowsItAndTheNewKeyMakesNothing()
    {
        var modifier = CommandsKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        foreach (var scope in new[] { CommandsScope.Global, CommandsScope.Apps })
        {
            var (vm, store, _, _) = Create(scope);
            var (bench, window) = Show(vm);
            var before = store.Current.AllCommands().Count();
            Assert.Empty(ShowingNewCommand(bench));

            // The keys are the tree's: with no row to focus, the focus is on its toolbar (as after a click on New category).
            Assert.True(bench.TreePart!.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_NewSection").Focus());
            window.KeyPressQwerty(PhysicalKey.N, modifier);

            Assert.Equal(before, store.Current.AllCommands().Count());
            Assert.Null(vm.SelectedSectionId);
            Assert.Equal(
                scope == CommandsScope.Global ? "Select a category first, or make one with New category." : "Select an app group first, or make one with New group",
                vm.Message);
        }
    }

#if DEBUG
    [AvaloniaFact]
    public void TheGalleryPagesShowItOnTheirSelectedSection()
    {
        var pages = new (ScreenDeclaration Page, string Section)[]
        {
            (DevGallery.CommandGalleryPages.GlobalWorkbenchPage(), "Window"),
            (DevGallery.CommandGalleryPages.AppsWorkbenchPage(), "Chrome"),
            (DevGallery.CommandGalleryPages.HoldRemapsPage(), "Space"),
        };
        foreach (var (page, section) in pages)
        {
            var bench = Assert.IsType<CommandsWorkbench>(Assert.IsType<ComponentScreen>(page).Build());
            new Window { Content = bench, Width = 1400, Height = 900 }.Show();

            Assert.Equal([section], ShowingNewCommand(bench));
        }
    }
#endif

    private static (CommandsWorkbench Bench, Window Window) Show(CommandsViewModel vm)
    {
        var bench = Assert.IsType<CommandsWorkbench>(Assert.IsType<ComponentScreen>(CommandsScreen.Declare(vm)).Build());
        var window = new Window { Content = bench, Width = 1400, Height = 900 };
        window.Show();
        return (bench, window);
    }

    private static List<string> ShowingNewCommand(CommandsWorkbench bench)
        => [.. bench.TreePart!.Rows.OfType<SectionRow>().Where(row => row.ShowsNewCommand).Select(row => row.NameText)];

    /// <summary>Clicks the header's New command as the pointer does, then lets the posted work (the rename editor) run.</summary>
    private static void ClickNewCommand(CommandsWorkbench bench, Window window, string section)
    {
        window.UpdateLayout();
        var row = bench.TreePart!.Rows.OfType<SectionRow>().Single(candidate => candidate.NameText == section);
        var button = row.GetVisualDescendants().OfType<Button>().Single(candidate => candidate.Name == "PART_NewCommand");
        Assert.True(button.IsEffectivelyVisible);
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
