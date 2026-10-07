using Augram.App.Components.CommandTree;
using Augram.App.ViewModels.Commands;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

public sealed class CommandHeaderTests
{
    [AvaloniaFact]
    public void KindDropdownAsksForTheKindAndFallsBackToTheCommandsRealKind()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);
        var (header, actions) = Show(Item(vm, "Close window"));
        Assert.True(header.HasCommand);
        Assert.True(header.IsGestureKind);
        Assert.Equal((int)TriggerKind.Gesture, header.KindIndex);

        Combo(header, "PART_TriggerKind").SelectedIndex = (int)TriggerKind.WheelDown;

        var kind = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.SetTriggerKind, kind.Action);
        Assert.Equal(TriggerKind.WheelDown, kind.Kind);
        Assert.Equal("Close window", kind.Command!.Name);
        Assert.Equal((int)TriggerKind.Gesture, header.KindIndex);

        Click(header.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_PickGesture"));
        Assert.Equal(CommandTreeAction.PickGesture, actions[^1].Action);

        header.Item = null;
        Assert.False(header.HasCommand);
        Assert.Equal(-1, header.KindIndex);
        Assert.Equal(-1, header.CategoryIndex);
    }

    [AvaloniaFact]
    public void UseOnBoxesAskForTheCommandsPlatformsAndShowTheCommandsRealOnes()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);
        var (header, actions) = Show(Item(vm, "Close window"));
        var windows = header.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "PART_UseOnWindows");
        var mac = header.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == "PART_UseOnMac");
        Assert.True(windows.IsChecked);
        Assert.True(mac.IsChecked);

        mac.IsChecked = false;

        var asked = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.SetUseOn, asked.Action);
        Assert.Equal(PlatformSet.Windows, asked.UseOn);
        Assert.True(mac.IsChecked);
    }

    [AvaloniaFact]
    public void CategoryDropdownOffersTheGroupsCategoriesAsksForTheChoiceAndStaysHonest()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);
        var (header, actions) = Show(Item(vm, "Close window"));
        var combo = Combo(header, "PART_Category");

        Assert.True(header.HasCategories);
        Assert.Equal(["Uncategorized", "Media", "Window"], combo.ItemsSource!.Cast<string>());
        Assert.Equal(2, header.CategoryIndex);

        combo.SelectedIndex = 1;

        var move = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.SetCategory, move.Action);
        Assert.Equal("Media", move.Category!.Name);
        Assert.Equal("Close window", move.Command!.Name);
        Assert.Equal(2, header.CategoryIndex);

        header.ChooseCategory(header.Item!.Categories[2]);
        Assert.Single(actions);

        header.Item = Item(vm, "Three steps");
        Assert.Equal(0, header.CategoryIndex);

        // An app group without categories has no dropdown; Photoshop has one, with its own categories.
        var (apps, _, _, _) = Create();
        header.Item = Item(apps, "Close tab");
        Assert.False(header.HasCategories);
        header.Item = Item(apps, "Brush");
        Assert.True(header.HasCategories);
        Assert.Equal(["Uncategorized", "Blend Mode Normal", "General"], combo.ItemsSource!.Cast<string>());
        Assert.Equal(2, header.CategoryIndex);
    }

    private static (CommandHeader Header, List<CommandTreeActionEventArgs> Actions) Show(CommandItem item)
    {
        var actions = new List<CommandTreeActionEventArgs>();
        var header = new CommandHeader { Item = item };
        header.ActionRequested += (_, e) => actions.Add(e);
        var window = new Window { Content = header };
        window.Show();
        return (header, actions);
    }

    private static ComboBox Combo(CommandHeader header, string name) => header.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == name);

    private static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
}
