using Augram.App.Components.CommandTree;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// "Use on" per Global category (Joel, 2026-10-08): the category form in the side panel (name, Use on; one undo step per
/// edit), the Global lists hiding or greying a category and its commands per platform, and the command header showing a box
/// the category or app group sets as disabled, with a note, while the command's own value is kept.
/// </summary>
public sealed class CommandsViewModelCategoryUseOnTests
{
    [AvaloniaFact]
    public void SelectingACategoryShowsItsForm_UncategorizedAndCommandsShowNone()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window")));
        var form = vm.GroupForm!;
        Assert.Equal("Window", Text("Name", form).Get());
        Assert.True(UseOn("Windows", form).Get());
        Assert.True(UseOn("macOS", form).Get());

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window"), Item(vm, "Minimize")));
        Assert.Null(vm.GroupForm);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, CategoryChoice.UncategorizedName)));
        Assert.Null(vm.GroupForm);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Media")));
        Assert.Equal("Media", Text("Name", vm.GroupForm!).Get());
    }

    [AvaloniaFact]
    public void RenamingInTheFormIsOneUndoStep_ARefusedNameShowsTheRuleAndKeepsTheText()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window")));
        var form = vm.GroupForm!;
        var name = Text("Name", form);

        name.Set("Media");
        Assert.Equal("A category named 'Media' already exists in 'Global'.", vm.Message);
        Assert.Equal("Media", name.Get());
        Assert.False(vm.CanUndo);

        name.Set("Windows");
        Assert.Null(vm.Message);
        Assert.Equal("Windows", Category(store, "Windows").Name);
        Assert.Same(form, vm.GroupForm);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal("Window", Category(store, "Window").Name);
        Assert.Equal("Window", name.Get());
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void UseOnInTheFormIsOneUndoStepAndLeavesTheCommandsOwnValues()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window")));
        var mac = UseOn("macOS", vm.GroupForm!);

        mac.Set(false);

        Assert.Equal(PlatformSet.Windows, Category(store, "Window").UseOn);
        Assert.Equal(PlatformSet.All, Find(store, "Minimize").UseOn);
        Assert.Equal("Window", Section(vm, "Window").Name);
        Assert.False(Section(vm, "Window").IsElsewhere);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(PlatformSet.All, Category(store, "Window").UseOn);
        Assert.True(mac.Get());
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void UntickingBothIsRefusedWithTheRule_AndTheBoxGoesBack()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window")));
        var form = vm.GroupForm!;
        UseOn("macOS", form).Set(false);

        UseOn("Windows", form).Set(false);

        Assert.Equal("Use 'Window' on at least one platform.", vm.Message);
        Assert.True(UseOn("Windows", form).Get());
        Assert.Equal(PlatformSet.Windows, Category(store, "Window").UseOn);
    }

    [AvaloniaFact]
    public void ACategoryNotUsedHereIsHiddenWithItsCommands_TheFilterShowsThemGreyedWithItsPlatform()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        SetCategoryUseOn(store, "Window", PlatformSet.Windows);

        Assert.Equal([CategoryChoice.UncategorizedName, "Media"], Names(vm));
        Assert.DoesNotContain(vm.Sections.SelectMany(section => section.Commands), command => command.Name is "Minimize" or "Close window");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleOtherPlatforms));

        var window = Section(vm, "Window");
        Assert.True(window.IsElsewhere);
        Assert.Equal("2 commands · Windows only", window.CountText);
        Assert.All(window.Commands, command => Assert.True(command is { IsElsewhere: true, PlatformMarker: "Windows only" }));
        Assert.False(Section(vm, "Media").IsElsewhere);
        Assert.False(Item(vm, "Volume up").IsElsewhere);
    }

    [AvaloniaFact]
    public void OnTheCategorysOwnPlatformItAndItsCommandsReadAsUsual()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        SetCategoryUseOn(store, "Window", PlatformSet.Windows);

        Assert.Equal([CategoryChoice.UncategorizedName, "Media", "Window"], Names(vm));
        var window = Section(vm, "Window");
        Assert.False(window.IsElsewhere);
        Assert.Equal("2 commands", window.CountText);
        Assert.All(window.Commands, command => Assert.False(command.IsElsewhere));
    }

    [AvaloniaFact]
    public void TheCommandHeaderDisablesTheBoxTheCategorySets_WithANote_AndKeepsTheCommandsOwnValue()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        SetCategoryUseOn(store, "Window", PlatformSet.Windows);
        var item = Item(vm, "Minimize");
        Assert.Equal(PlatformSet.All, item.UseOn);
        Assert.Equal(PlatformSet.Windows, item.UseOnLimit);
        Assert.Equal("Set by category 'Window': Windows only", item.UseOnLimitText);

        var (header, actions) = Show(item);
        var windows = Box(header, "PART_UseOnWindows");
        var mac = Box(header, "PART_UseOnMac");

        Assert.Equal("Set by category 'Window': Windows only", header.UseOnNote);
        Assert.True(windows is { IsEnabled: true, IsChecked: true });
        Assert.True(mac is { IsEnabled: false, IsChecked: false });

        header.ChooseUseOn(HostPlatform.MacOS, true);
        Assert.Empty(actions);

        windows.IsChecked = false;
        var asked = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.SetUseOn, asked.Action);
        Assert.Equal(PlatformSet.MacOS, asked.UseOn);

        header.Item = Item(vm, "Volume up");
        Assert.Null(header.UseOnNote);
        Assert.True(mac is { IsEnabled: true, IsChecked: true });
    }

    [AvaloniaFact]
    public void ReEnablingTheCategoryRestoresTheCommandsOwnUseOn()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleOtherPlatforms));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetUseOn, Section(vm, "Window"), Item(vm, "Minimize"), useOn: PlatformSet.MacOS));
        SetCategoryUseOn(store, "Window", PlatformSet.Windows);
        Assert.Equal(PlatformSet.MacOS, Find(store, "Minimize").UseOn);
        Assert.Equal("used nowhere", Item(vm, "Minimize").PlatformMarker);

        SetCategoryUseOn(store, "Window", PlatformSet.All);

        Assert.Equal(PlatformSet.MacOS, Item(vm, "Minimize").UseOn);
        Assert.Null(Item(vm, "Minimize").UseOnLimitText);
    }

    [AvaloniaFact]
    public void AnAppGroupsUseOnIsNamedInTheHeaderNoteToo()
    {
        var (vm, store, _, _) = Create(platform: HostPlatform.Windows);
        store.UpdateGroup(Group(store, "Photoshop") with { UseOn = PlatformSet.Windows });

        Assert.Equal("Set by app group 'Photoshop': Windows only", Item(vm, "Brush").UseOnLimitText);
        Assert.Null(Item(vm, "Close tab").UseOnLimitText);
    }

    private static void SetCategoryUseOn(MappingStore store, string name, PlatformSet useOn)
        => store.UpdateGroup(store.Global with { Categories = [.. store.Global.Categories.Select(category => category.Name == name ? category with { UseOn = useOn } : category)] });

    private static IValueBinding<string> Text(string label, FormScreen screen)
        => (IValueBinding<string>)screen.Sections.SelectMany(section => section.Fields).Single(field => field.Label == label).Binding!;

    private static IValueBinding<bool> UseOn(string platform, FormScreen screen)
        => screen.Sections.SelectMany(section => section.Fields).OfType<TogglesField>().Single(field => field.Label == "Use on").Options.Single(option => option.Caption == platform).Value;

    private static (CommandHeader Header, List<CommandTreeActionEventArgs> Actions) Show(CommandItem item)
    {
        var actions = new List<CommandTreeActionEventArgs>();
        var header = new CommandHeader { Item = item };
        header.ActionRequested += (_, e) => actions.Add(e);
        var window = new Window { Content = header };
        window.Show();
        return (header, actions);
    }

    private static CheckBox Box(CommandHeader header, string name) => header.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == name);
}
