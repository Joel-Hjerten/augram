using Augram.App.Components.CommandTree;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
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
        Assert.Equal(["Gesture", "Wheel", "No trigger"], Combo(header, "PART_TriggerKind").ItemsSource!.Cast<string>());
        Assert.Equal(IndexOf(TriggerKind.Gesture), header.KindIndex);

        Combo(header, "PART_TriggerKind").SelectedIndex = IndexOf(TriggerKind.Wheel);

        var kind = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.SetTriggerKind, kind.Action);
        Assert.Equal(TriggerKind.Wheel, kind.Kind);
        Assert.Equal("Close window", kind.Command!.Name);
        Assert.Equal(IndexOf(TriggerKind.Gesture), header.KindIndex);

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

    [AvaloniaFact]
    public void AWheelTriggerSaysHowItFires_AGestureNeedsNoHint()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);
        var (header, _) = Show(Item(vm, "Close window"));
        Assert.Null(header.TriggerHint);

        header.Item = Item(vm, "Volume up");

        Assert.Equal("Hold the stroke button and turn the mouse wheel up; every notch fires.", header.TriggerHint);
    }

    /// <summary>F1 combinations (Joel, 2026-10-09): Up / Down beside Wheel; "While holding" buttons and keys; the stroke box locked for a gesture.</summary>
    [AvaloniaFact]
    public void HoldBoxesAskForTheSetAndShowTheCommandsRealOne()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);
        var (header, actions) = Show(Item(vm, "Volume up"));
        Assert.True(header.IsWheelKind);
        Assert.Equal(0, header.WheelIndex);
        Assert.False(header.HasHoldMembers);
        Assert.Equal(["Ctrl", "Alt", "Shift", "Win"], KeyBoxes(header).Select(box => (string)box.Content!));
        var stroke = Box(header, "PART_HoldStroke");
        Assert.True(stroke.IsChecked);
        Assert.True(stroke.IsEnabled);

        Box(header, "PART_KeyShift").IsChecked = true;
        var shift = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.SetTriggerHold, shift.Action);
        Assert.Equal(TriggerHold.WithStroke(KeyModifiers.Shift), shift.Hold);
        Assert.False(Box(header, "PART_KeyShift").IsChecked);

        stroke.IsChecked = false;
        Box(header, "PART_HoldRight").IsChecked = true;
        Assert.Equal([new TriggerHold(HeldButtons.None), new TriggerHold(HeldButtons.Stroke | HeldButtons.Right)], actions.Skip(1).Select(action => action.Hold));

        Combo(header, "PART_WheelDirection").SelectedIndex = 1;
        Assert.Equal((CommandTreeAction.SetWheelDirection, WheelDirection.Down), (actions[^1].Action, actions[^1].Wheel!.Value));
        Assert.Equal(0, header.WheelIndex);

        header.Item = Item(vm, "Close window");
        Assert.False(header.IsWheelKind);
        Assert.True(Box(header, "PART_HoldStroke").IsChecked);
        Assert.False(Box(header, "PART_HoldStroke").IsEnabled);
        Assert.Null(header.AnchorWarning);
    }

    [AvaloniaFact]
    public void ACombinationShowsTheCaptureChoice_AndAnAnchorSaysWhichClicksWait()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);
        var volume = Item(vm, "Volume up");
        var (header, actions) = Show(volume with { Trigger = Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Left, KeyModifiers.Control)), AnchorWarning = "Left clicks in every app wait until you release or move." });

        Assert.True(header.HasHoldMembers);
        Assert.False(Box(header, "PART_HoldStroke").IsChecked);
        Assert.True(Box(header, "PART_HoldLeft").IsChecked);
        Assert.True(Box(header, "PART_KeyControl").IsChecked);
        Assert.Equal("Left clicks in every app wait until you release or move.", header.AnchorWarning);

        Combo(header, "PART_Capture").SelectedIndex = 1;
        Assert.Equal(new TriggerHold(HeldButtons.Left, KeyModifiers.Control, HoldCapture.Before), Assert.Single(actions).Hold);
        Assert.Equal(0, Combo(header, "PART_Capture").SelectedIndex);
    }

    private static int IndexOf(TriggerKind kind) => TriggerKindExtensions.All.ToList().IndexOf(kind);

    private static CheckBox Box(CommandHeader header, string name) => header.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == name);

    private static IEnumerable<CheckBox> KeyBoxes(CommandHeader header)
        => new[] { "PART_KeyControl", "PART_KeyAlt", "PART_KeyShift", "PART_KeyMeta" }.Select(name => Box(header, name));

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
