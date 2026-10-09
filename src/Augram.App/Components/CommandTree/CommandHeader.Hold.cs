using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The trigger-combination half of <see cref="CommandHeader"/> (F1 "Triggers as combinations", Joel 2026-10-09): the wheel
/// direction beside the kind (<c>PART_WheelDirection</c>: Up / Down), the "While holding" boxes for the buttons
/// (<c>PART_HoldStroke</c>, <c>PART_HoldLeft</c>, <c>PART_HoldRight</c>, <c>PART_HoldMiddle</c>, <c>PART_HoldX1</c>,
/// <c>PART_HoldX2</c>) and the keys (<c>PART_KeyControl</c>, <c>PART_KeyAlt</c>, <c>PART_KeyShift</c>, <c>PART_KeyMeta</c>,
/// labelled Ctrl / Alt / Shift / Win, or Ctrl / Opt / Shift / Cmd on a Mac, through <see cref="HotkeyText"/>), and when they
/// must have gone down (<c>PART_Capture</c>, shown once something besides the anchor is held). The stroke button's box is
/// ticked and locked for a gesture or a click (it draws, it clicks) and free for a wheel trigger. Every box only asks
/// (<see cref="CommandTreeAction.SetTriggerHold"/>); <see cref="ApplyHold"/> then shows what is really stored.
/// </summary>
public sealed partial class CommandHeader
{
    public static readonly StyledProperty<bool> IsWheelKindProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(IsWheelKind));

    public static readonly StyledProperty<bool> HasHoldMembersProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(HasHoldMembers));

    public static readonly StyledProperty<string?> AnchorWarningProperty =
        AvaloniaProperty.Register<CommandHeader, string?>(nameof(AnchorWarning));

    public static readonly StyledProperty<string?> TriggerNoteProperty =
        AvaloniaProperty.Register<CommandHeader, string?>(nameof(TriggerNote));

    private static readonly (string Part, HeldButtons Flag)[] ButtonParts =
    [
        ("PART_HoldStroke", HeldButtons.Stroke),
        ("PART_HoldLeft", HeldButtons.Left),
        ("PART_HoldRight", HeldButtons.Right),
        ("PART_HoldMiddle", HeldButtons.Middle),
        ("PART_HoldX1", HeldButtons.X1),
        ("PART_HoldX2", HeldButtons.X2),
    ];

    private static readonly (string Part, KeyModifiers Flag)[] KeyParts =
    [
        ("PART_KeyControl", KeyModifiers.Control),
        ("PART_KeyAlt", KeyModifiers.Alt),
        ("PART_KeyShift", KeyModifiers.Shift),
        ("PART_KeyMeta", KeyModifiers.Meta),
    ];

    private readonly Dictionary<HeldButtons, CheckBox> _buttonBoxes = [];
    private readonly Dictionary<KeyModifiers, CheckBox> _keyBoxes = [];
    private AskingDropdown? _wheel;
    private AskingDropdown? _capture;

    /// <summary>The labels of the wheel direction choice, in <see cref="TriggerKindExtensions.Directions"/> order.</summary>
    public static IReadOnlyList<string> DirectionLabels { get; } = [.. TriggerKindExtensions.Directions.Select(direction => direction.Label())];

    /// <summary>The labels of the capture choice, in <see cref="TriggerKindExtensions.Captures"/> order.</summary>
    public static IReadOnlyList<string> CaptureLabels { get; } = [.. TriggerKindExtensions.Captures.Select(capture => capture.Label())];

    /// <summary>The key box labels on this platform, Ctrl / Alt / Shift / Win or Ctrl / Opt / Shift / Cmd (<see cref="HotkeyText.Names"/>).</summary>
    public static string KeyLabel(KeyModifiers key) => HotkeyText.Format(key, KeyCode.None);

    /// <summary>Shows the Up / Down choice: the trigger is a wheel trigger.</summary>
    public bool IsWheelKind
    {
        get => GetValue(IsWheelKindProperty);
        private set => SetValue(IsWheelKindProperty, value);
    }

    /// <summary>Shows the capture choice: the trigger holds keys, or buttons besides its anchor.</summary>
    public bool HasHoldMembers
    {
        get => GetValue(HasHoldMembersProperty);
        private set => SetValue(HasHoldMembersProperty, value);
    }

    /// <summary>Which clicks now wait for release or a move (an anchor other than the stroke button; <see cref="CommandItem.AnchorWarning"/>).</summary>
    public string? AnchorWarning
    {
        get => GetValue(AnchorWarningProperty);
        private set => SetValue(AnchorWarningProperty, value);
    }

    /// <summary>F8 for the trigger (<see cref="CommandItem.TriggerNote"/>): converted from the other platform, own here, or none here.</summary>
    public string? TriggerNote
    {
        get => GetValue(TriggerNoteProperty);
        private set => SetValue(TriggerNoteProperty, value);
    }

    /// <summary>The direction the wheel choice shows; -1 without a wheel trigger.</summary>
    public int WheelIndex => _wheel?.SelectedIndex ?? -1;

    /// <summary>What a "While holding" box or the capture choice does: asks the host for the command's trigger to hold <paramref name="hold"/>.</summary>
    public void ChooseHold(TriggerHold hold)
    {
        ArgumentNullException.ThrowIfNull(hold);
        if (Item is { } item && hold != item.Trigger.Hold)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerHold, command: item, hold: hold));
        }

        ApplyHold();
    }

    /// <summary>What the Up / Down choice does: asks the host for the other direction.</summary>
    public void ChooseWheel(WheelDirection direction)
    {
        if (Item is { Trigger: Trigger.WheelTrigger wheel } item && wheel.Direction != direction)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetWheelDirection, command: item, wheel: direction));
        }

        ApplyHold();
    }

    private void FindHoldParts(TemplateAppliedEventArgs e)
    {
        _buttonBoxes.Clear();
        _keyBoxes.Clear();
        foreach (var (part, flag) in ButtonParts)
        {
            if (e.NameScope.Find<CheckBox>(part) is { } box)
            {
                _buttonBoxes[flag] = box;
                box.IsCheckedChanged += (_, _) => Toggled(hold => hold with { Buttons = Flip(hold.Buttons, flag, box.IsChecked == true) });
            }
        }

        foreach (var (part, flag) in KeyParts)
        {
            if (e.NameScope.Find<CheckBox>(part) is { } box)
            {
                box.Content = KeyLabel(flag);
                _keyBoxes[flag] = box;
                box.IsCheckedChanged += (_, _) => Toggled(hold => hold with { Keys = box.IsChecked == true ? hold.Keys | flag : hold.Keys & ~flag });
            }
        }

        if (e.NameScope.Find<ComboBox>("PART_WheelDirection") is { } wheel)
        {
            _wheel = new AskingDropdown(wheel, index => ChooseWheel(TriggerKindExtensions.Directions[index]));
        }

        if (e.NameScope.Find<ComboBox>("PART_Capture") is { } capture)
        {
            _capture = new AskingDropdown(capture, index =>
            {
                if (Item is { } item)
                {
                    ChooseHold(item.Trigger.Hold with { Capture = TriggerKindExtensions.Captures[index] });
                }
            });
        }
    }

    private static HeldButtons Flip(HeldButtons buttons, HeldButtons flag, bool on) => on ? buttons | flag : buttons & ~flag;

    private void Toggled(Func<TriggerHold, TriggerHold> change)
    {
        if (!_applying && Item is { } item)
        {
            ChooseHold(change(item.Trigger.Hold));
        }
    }

    /// <summary>Puts the boxes and the two choices on the command's real trigger; the stroke box is locked unless it is a wheel trigger.</summary>
    private void ApplyHold()
    {
        var item = Item;
        var trigger = item?.Trigger ?? Trigger.None;
        var hold = trigger.Hold;
        var isWheel = trigger is Trigger.WheelTrigger;
        IsWheelKind = isWheel;
        HasHoldMembers = trigger.IsBound && hold.HasMembers;
        _applying = true;
        try
        {
            foreach (var (flag, box) in _buttonBoxes)
            {
                var stroke = flag == HeldButtons.Stroke;
                box.IsEnabled = item is not null && (!stroke || isWheel);
                box.IsChecked = (stroke && !isWheel) || (hold.Buttons & flag) != 0;
            }

            foreach (var (flag, box) in _keyBoxes)
            {
                box.IsEnabled = item is not null;
                box.IsChecked = (hold.Keys & flag) != 0;
            }
        }
        finally
        {
            _applying = false;
        }

        _wheel?.Show(DirectionLabels, trigger is Trigger.WheelTrigger wheel ? TriggerKindExtensions.Directions.ToList().IndexOf(wheel.Direction) : -1);
        _capture?.Show(CaptureLabels, item is null ? -1 : TriggerKindExtensions.Captures.ToList().IndexOf(hold.Capture));
    }
}
