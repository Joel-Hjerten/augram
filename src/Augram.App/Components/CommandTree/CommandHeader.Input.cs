using Augram.App.Components.ButtonDetect;
using Augram.App.Components.HotkeyCapture;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The Input half of <see cref="CommandHeader"/> (F9, plan 0002 step 4): for a command under a hold remap
/// (<see cref="CommandItem.IsUnderHoldRemap"/>) the header shows <see cref="IsInputCommand"/> rows in place of the trigger kind
/// and the "While holding" boxes. <c>PART_InputKind</c> (Buttons / Wheel / Key / No input) asks for the kind; for buttons, one
/// chip per button of the set in <c>PART_InputButtons</c> (a click removes it) and <c>PART_AddButton</c>, which detects the
/// next press (<see cref="ButtonDetector"/>: the engine, else this window) and adds it, so several make a set; for a wheel,
/// <c>PART_InputWheel</c> (Up / Down); for a key, <c>PART_InputKey</c>, the capture field in one-key mode, which refuses
/// modifiers and the hold key with Core's reason (<see cref="HoldRemapRules.InputKeyProblem"/>). Everything only asks
/// (<see cref="CommandTreeAction.SetInputKind"/>, <see cref="CommandTreeAction.SetInput"/>) and then shows the item's input:
/// the stored one, or the host's draft while it is not valid yet, with <see cref="DraftNote"/> saying why.
/// </summary>
public sealed partial class CommandHeader
{
    public static readonly StyledProperty<bool> IsInputCommandProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(IsInputCommand));

    public static readonly StyledProperty<bool> IsButtonsInputProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(IsButtonsInput));

    public static readonly StyledProperty<bool> IsWheelInputProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(IsWheelInput));

    public static readonly StyledProperty<bool> IsKeyInputProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(IsKeyInput));

    public static readonly StyledProperty<bool> IsDetectingProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(IsDetecting));

    public static readonly StyledProperty<string> InputStatusProperty =
        AvaloniaProperty.Register<CommandHeader, string>(nameof(InputStatus), string.Empty);

    private ButtonDetector? _detector;
    private CommandId? _detectingFor;
    private AskingDropdown? _inputKind;
    private AskingDropdown? _inputWheel;
    private Panel? _inputButtons;
    private HotkeyCaptureBox? _inputKey;

    /// <summary>The labels of the Input dropdown, in <see cref="InputKindExtensions.All"/> order.</summary>
    public static IReadOnlyList<string> InputKindLabels { get; } = [.. InputKindExtensions.All.Select(kind => kind.Label())];

    /// <summary>A command under a hold remap: the Input rows show, the trigger rows do not.</summary>
    public bool IsInputCommand
    {
        get => GetValue(IsInputCommandProperty);
        private set => SetValue(IsInputCommandProperty, value);
    }

    public bool IsButtonsInput
    {
        get => GetValue(IsButtonsInputProperty);
        private set => SetValue(IsButtonsInputProperty, value);
    }

    public bool IsWheelInput
    {
        get => GetValue(IsWheelInputProperty);
        private set => SetValue(IsWheelInputProperty, value);
    }

    public bool IsKeyInput
    {
        get => GetValue(IsKeyInputProperty);
        private set => SetValue(IsKeyInputProperty, value);
    }

    /// <summary>Add button… is waiting for a press.</summary>
    public bool IsDetecting
    {
        get => GetValue(IsDetectingProperty);
        private set => SetValue(IsDetectingProperty, value);
    }

    /// <summary>What the detection says: the prompt while it waits, or that no press came.</summary>
    public string InputStatus
    {
        get => GetValue(InputStatusProperty);
        private set => SetValue(InputStatusProperty, value);
    }

    /// <summary>Explicit button capture for Add button… (tests, gallery); null looks up the application resource.</summary>
    public IButtonCapture? ButtonCapture { get; set; }

    /// <summary>The input kind the dropdown shows; -1 without a command under a hold remap.</summary>
    public int InputKindIndex => _inputKind?.SelectedIndex ?? -1;

    /// <summary>The buttons the chips show, in display order.</summary>
    public IReadOnlyList<MouseButton> InputButtons => ShownButtons(Item).Buttons().ToList();

    /// <summary>What the Input dropdown does: asks the host for the kind (it keeps a not-yet-valid one as a draft).</summary>
    public void ChooseInputKind(InputKind kind)
    {
        if (Item is { IsUnderHoldRemap: true } item && kind != InputKindExtensions.KindOf(item.Trigger))
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetInputKind, command: item, inputKind: kind));
        }

        ApplyInput();
    }

    /// <summary>What a chip, a detected button, the direction or the key field does: asks the host for <paramref name="input"/>.</summary>
    public void ChooseInput(HoldInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (Item is { IsUnderHoldRemap: true } item && Trigger.ForInput(input) != item.Trigger)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetInput, command: item, input: input));
        }

        ApplyInput();
    }

    /// <summary>What a button chip does: the set without that button (empty waits as a draft until a button is added).</summary>
    public void RemoveButton(MouseButton button) => ChooseInput(new HoldInput.Buttons(ShownButtons(Item) & ~button.Flag()));

    /// <summary>What a detected press does: the set with that button added.</summary>
    public void AddButton(MouseButton button) => ChooseInput(new HoldInput.Buttons(ShownButtons(Item) | button.Flag()));

    /// <summary>Add button…: waits for the next press (anywhere with the engine, on this window without) and adds it.</summary>
    public void DetectButton()
    {
        if (Item is not { IsUnderHoldRemap: true } item)
        {
            return;
        }

        _detector ??= NewDetector();
        _detector.Capture = ButtonCapture;
        _detectingFor = item.Id;
        _detector.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _detector?.Stop();
    }

    private static HeldButtons ShownButtons(CommandItem? item)
        => item?.Trigger is Trigger.InputTrigger { Input: HoldInput.Buttons { Set: var set } } ? set & HeldButtonsExtensions.Physical : HeldButtons.None;

    private ButtonDetector NewDetector()
    {
        var detector = new ButtonDetector(this, button =>
        {
            if (Item?.Id == _detectingFor)
            {
                AddButton(button);
            }
        });
        detector.Changed += (_, _) =>
        {
            IsDetecting = detector.IsListening;
            InputStatus = detector.Status;
        };
        return detector;
    }

    private void FindInputParts(TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<ComboBox>("PART_InputKind") is { } kind)
        {
            _inputKind = new AskingDropdown(kind, index => ChooseInputKind(InputKindExtensions.All[index]));
        }

        if (e.NameScope.Find<ComboBox>("PART_InputWheel") is { } wheel)
        {
            _inputWheel = new AskingDropdown(wheel, index => ChooseInput(new HoldInput.Wheel(TriggerKindExtensions.Directions[index])));
        }

        _inputButtons = e.NameScope.Find<Panel>("PART_InputButtons");
        if (e.NameScope.Find<Button>("PART_AddButton") is { } add)
        {
            add.Click += (_, _) => DetectButton();
        }

        _inputKey = e.NameScope.Find<HotkeyCaptureBox>("PART_InputKey");
        if (_inputKey is not null)
        {
            _inputKey.SingleKey = true;
            _inputKey.KeyProblem = key => Item?.HoldRemap is { } holdRemap ? HoldRemapRules.InputKeyProblem(key, holdRemap) : null;
            _inputKey.Committed += (_, committed) => ChooseInput(new HoldInput.Key(committed.Key));
        }
    }

    /// <summary>Puts the Input rows on the item's input (stored, or the draft): the kind, the chips, the direction, the key.</summary>
    private void ApplyInput()
    {
        var item = Item;
        if (_detector is { IsListening: true } && item?.Id != _detectingFor)
        {
            _detector.Stop();
        }

        var trigger = item?.Trigger ?? Trigger.None;
        var underHoldRemap = item is { IsUnderHoldRemap: true };
        var kind = InputKindExtensions.KindOf(trigger);
        IsInputCommand = underHoldRemap;
        IsButtonsInput = underHoldRemap && kind == InputKind.Buttons;
        IsWheelInput = underHoldRemap && kind == InputKind.Wheel;
        IsKeyInput = underHoldRemap && kind == InputKind.Key;
        _inputKind?.Show(InputKindLabels, underHoldRemap ? InputKindExtensions.All.ToList().IndexOf(kind) : -1);
        var input = (trigger as Trigger.InputTrigger)?.Input;
        _inputWheel?.Show(DirectionLabels, input is HoldInput.Wheel wheel ? TriggerKindExtensions.Directions.ToList().IndexOf(wheel.Direction) : -1);
        if (_inputKey is not null)
        {
            _inputKey.Key = input is HoldInput.Key key ? key.KeyCode : KeyCode.None;
        }

        ShowChips(ShownButtons(item));
    }

    private void ShowChips(HeldButtons set)
    {
        if (_inputButtons is null)
        {
            return;
        }

        _inputButtons.Children.Clear();
        foreach (var button in set.Buttons())
        {
            var chip = new Button { Content = $"{button} ×", Name = "PART_Chip" + button };
            chip.Classes.Add("toolbar");
            ToolTip.SetTip(chip, $"Remove {button} from the input");
            chip.Click += (_, _) => RemoveButton(button);
            _inputButtons.Children.Add(chip);
        }
    }
}
