using Augram.App.Components.CommandTree;
using Augram.App.Components.ListEditing;
using Augram.App.Components.SearchBox;
using Augram.App.Components.Shell;
using Augram.App.Declarations;
using Augram.App.Navigation;
using Augram.App.Views;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Components;

/// <summary>
/// The find key works from anywhere on a tab (plan 0009 decision 6; Joel, Mac, 0.13.11: it needed a click in the list
/// first): the shell binds it on the window, which is all Avalonia evaluates while nothing has the focus.
/// </summary>
public sealed class ShellFindKeyTests
{
    private static RawInputModifiers Modifier => EditKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;

    [AvaloniaFact]
    public void WithNothingFocusedTheFindKeyFocusesTheShownTabsSearchBox()
    {
        var window = TestAppBuilder.Services.GetRequiredService<MainWindow>();
        window.Show();
        window.UpdateLayout();
        var shell = window.GetVisualDescendants().OfType<Shell>().Single();
        shell.SelectedKey = AppNavigation.CommandsGlobalKey;
        window.UpdateLayout();
        window.FocusManager!.ClearFocus();
        Assert.Null(window.FocusManager.GetFocusedElement());

        window.KeyPressQwerty(PhysicalKey.F, Modifier);

        var box = Assert.Single(shell.GetVisualDescendants().OfType<SearchBox>(), box => box.IsEffectivelyVisible);
        Assert.True(box.IsTyping);

        // Options has no search box: the key does nothing there.
        shell.SelectedKey = AppNavigation.OptionsGeneralKey;
        window.UpdateLayout();
        window.FocusManager.ClearFocus();
        window.KeyPressQwerty(PhysicalKey.F, Modifier);
        Assert.Null(window.FocusManager.GetFocusedElement());
    }

    [AvaloniaFact]
    public void AFocusedListAnswersTheFindKeyItselfAndARenameEditorKeepsIt()
    {
        var (vm, _, _, _) = Create();
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(vm, "Chrome")));
        var tree = new CommandTree { Sections = vm.Sections, Heading = vm.Heading };
        var shell = new Shell { Registry = new NavigationRegistry([new NavEntry("Commands", "commands", () => new ComponentScreen("Commands", () => tree))]) };
        var window = new Window { Content = shell, Width = 800, Height = 600 };
        window.Show();
        Render();
        var fired = 0;
        var binding = Assert.Single(window.KeyBindings);
        var shellFind = binding.Command;
        binding.Command = new RelayCommand(() => { fired++; shellFind.Execute(null); }, () => shellFind.CanExecute(null));
        var box = tree.GetVisualDescendants().OfType<SearchBox>().Single();

        // A focused row: the tree's own binding handles the key, the shell's is never reached.
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        tree.SelectedCommandId = Item(vm, "Close tab").Id;
        var list = tree.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.True(list.ContainerFromItem(list.SelectedItem!)!.Focus());
        window.KeyPressQwerty(PhysicalKey.F, Modifier);
        Assert.True(box.IsTyping);
        Assert.Equal(0, fired);

        // A rename editor: neither the tree's nor the shell's binding runs, so typing a name is not interrupted.
        tree.BeginRename(Item(vm, "Close tab").Id);
        var editor = tree.Rows.OfType<CommandRow>().Single(row => row.NameText == "Close tab").GetVisualDescendants().OfType<TextBox>().Single();
        Assert.True(editor.IsFocused);
        window.KeyPressQwerty(PhysicalKey.F, Modifier);
        Assert.True(editor.IsFocused);
        Assert.False(box.IsTyping);
        Assert.Equal(0, fired);

        // Nothing focused: the shell's binding is what answers.
        window.FocusManager!.ClearFocus();
        window.KeyPressQwerty(PhysicalKey.F, Modifier);
        Assert.True(box.IsTyping);
        Assert.Equal(1, fired);
    }

    private static void Render()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
