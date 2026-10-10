using Augram.App.Components.ButtonDetect;
using Augram.App.Components.CommandTree;
using Augram.App.Components.HotkeyCapture;
using Augram.App.Tests.Steps;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;
using static Augram.App.Tests.Commands.HoldRemapTestData;
using MouseButton = Augram.Core.Capture.MouseButton;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The header's Input rows for a command under a hold remap (F9, plan 0002 step 4): they replace the trigger kind and the
/// "While holding" boxes; buttons are chips (a click removes one) added by detect-to-assign (a fake engine, or this window
/// under --no-engine); a wheel has its direction; a key is the capture field in one-key mode, which refuses the hold key and
/// modifiers with Core's reason. Everything only asks.
/// </summary>
public sealed class CommandHeaderInputTests
{
    [AvaloniaFact]
    public void ACommandUnderAHoldRemapShowsItsInputInsteadOfTheTrigger_ChipsAskToRemoveAButton()
    {
        var (vm, _, _) = CreateBlender();
        var (header, actions) = Show(Item(vm, "Zoom both"));

        Assert.True(header.IsInputCommand);
        Assert.True(header.IsButtonsInput);
        Assert.False(Part<ComboBox>(header, "PART_TriggerKind").IsEffectivelyVisible);
        Assert.False(Part<CheckBox>(header, "PART_HoldLeft").IsEffectivelyVisible);
        Assert.Equal(["Buttons", "Wheel", "Key", "No input"], Part<ComboBox>(header, "PART_InputKind").ItemsSource!.Cast<string>());
        Assert.Equal(0, header.InputKindIndex);
        Assert.Equal([MouseButton.Left, MouseButton.Right], header.InputButtons);
        Assert.Equal("Fires while Space is held.", header.TriggerHint);

        Click(Part<Button>(header, "PART_ChipRight"));

        var removed = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.SetInput, removed.Action);
        Assert.Equal(HoldInput.Of(MouseButton.Left), removed.Input);
        Assert.Equal([MouseButton.Left, MouseButton.Right], header.InputButtons);

        Part<ComboBox>(header, "PART_InputKind").SelectedIndex = 2;
        Assert.Equal((CommandTreeAction.SetInputKind, InputKind.Key), (actions[^1].Action, actions[^1].InputKind!.Value));
        Assert.Equal(0, header.InputKindIndex);

        header.Item = Item(vm, "Undo");
        Assert.False(header.IsInputCommand);
        Assert.True(Part<ComboBox>(header, "PART_TriggerKind").IsEffectivelyVisible);
        Assert.Equal(-1, header.InputKindIndex);
    }

    [AvaloniaFact]
    public void AddButtonDetectsTheNextPressAndAsksForTheSetWithIt()
    {
        var (vm, _, _) = CreateBlender();
        var capture = new FakeButtonCapture();
        var (header, actions) = Show(Item(vm, "Orbit"), capture);

        Click(Part<Button>(header, "PART_AddButton"));

        Assert.True(header.IsDetecting);
        Assert.Equal(ButtonDetector.ListeningText, header.InputStatus);
        Assert.False(Part<Button>(header, "PART_AddButton").IsEnabled);
        capture.Press(MouseButton.Middle);

        Assert.Equal(HoldInput.Of(MouseButton.Left, MouseButton.Middle), Assert.Single(actions).Input);
        Assert.False(header.IsDetecting);
        Assert.False(capture.IsArmed);
        Assert.Empty(header.InputStatus);

        // Another command selected meanwhile: the detection ends and the press is nobody's.
        Click(Part<Button>(header, "PART_AddButton"));
        header.Item = Item(vm, "Pan");
        Assert.False(header.IsDetecting);
        capture.Press(MouseButton.X1);
        Assert.Single(actions);
    }

    [AvaloniaFact]
    public void WithoutTheEngineAPressOnThisWindowIsTheButton()
    {
        var (vm, _, _) = CreateBlender();
        var (header, actions) = Show(Item(vm, "Orbit"), new FakeButtonCapture { Available = false });
        var window = (Window)TopLevel.GetTopLevel(header)!;

        Click(Part<Button>(header, "PART_AddButton"));
        Assert.Equal(ButtonDetector.WindowOnlyText, header.InputStatus);
        window.MouseDown(new Point(5, 5), Avalonia.Input.MouseButton.Right);
        window.MouseUp(new Point(5, 5), Avalonia.Input.MouseButton.Right);

        Assert.Equal(HoldInput.Of(MouseButton.Left, MouseButton.Right), Assert.Single(actions).Input);
        Assert.False(header.IsDetecting);
    }

    [AvaloniaFact]
    public void TheKeyFieldTakesOneKey_RefusingTheHoldKeyAndModifiersWithTheRule()
    {
        var (vm, _, _) = CreateBlender();
        var (header, actions) = Show(Item(vm, "Grab"));
        var box = Part<HotkeyCaptureBox>(header, "PART_InputKey");
        var keys = new FakeKeyCapture();
        box.KeyCapture = keys;

        Assert.True(header.IsKeyInput);
        Assert.True(box.SingleKey);
        Assert.Equal(("W", KeyCode.W), (box.DisplayText, box.Key));

        box.BeginCapture();
        keys.Press(KeyCode.Space);
        Assert.Equal("Space cannot be an input of 'Space': it is the hold key of 'Space'.", box.StatusText);
        keys.Press(KeyCode.LeftControl);
        Assert.Equal("Left Ctrl cannot be an input of 'Space': Ctrl, Alt, Shift and Win pass through while a hold key is held.", box.StatusText);
        keys.Press(KeyCode.E, KeyModifiers.Control);
        box.Accept();

        Assert.Equal(new HoldInput.Key(KeyCode.E), Assert.Single(actions).Input);
        Assert.Equal(KeyCode.W, box.Key);
    }

    [AvaloniaFact]
    public void AWheelInputShowsItsDirectionAndAsksForTheOther()
    {
        var (vm, _, _) = CreateBlender();
        var (header, actions) = Show(Item(vm, "Orbit") with { Trigger = Trigger.ForInput(new HoldInput.Wheel(WheelDirection.Down)) });

        Assert.True(header.IsWheelInput);
        Assert.Equal(1, Part<ComboBox>(header, "PART_InputWheel").SelectedIndex);

        Part<ComboBox>(header, "PART_InputWheel").SelectedIndex = 0;

        Assert.Equal(new HoldInput.Wheel(WheelDirection.Up), Assert.Single(actions).Input);
        Assert.Equal(1, Part<ComboBox>(header, "PART_InputWheel").SelectedIndex);
    }

    private static (CommandHeader Header, List<CommandTreeActionEventArgs> Actions) Show(CommandItem item, IButtonCapture? capture = null)
    {
        var actions = new List<CommandTreeActionEventArgs>();
        var header = new CommandHeader { Item = item, ButtonCapture = capture ?? new FakeButtonCapture() };
        header.ActionRequested += (_, e) => actions.Add(e);
        new Window { Content = header, Width = 1000, Height = 700 }.Show();
        return (header, actions);
    }

    private static T Part<T>(CommandHeader header, string name)
        where T : Control
        => header.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
}
