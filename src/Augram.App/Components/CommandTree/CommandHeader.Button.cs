using Augram.Core.Capture;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The button trigger half of <see cref="CommandHeader"/> (plan 0005, Joel 2026-10-10: "Right + Left", Eyeris's loupe chord in
/// Augram): for the Button kind, <c>PART_TriggerButton</c> beside the kind dropdown chooses the button pressed (Left, Right,
/// Middle, X1, X2), as the wheel's Up / Down sits beside Wheel. The buttons held are the "While holding" boxes, where the
/// stroke button's box and the pressed button's are locked unticked (<c>ApplyHold</c>). It only asks
/// (<see cref="CommandTreeAction.SetTriggerButton"/>) and then shows the item's trigger: the stored one, or the host's draft.
/// </summary>
public sealed partial class CommandHeader
{
    public static readonly StyledProperty<bool> IsButtonKindProperty =
        AvaloniaProperty.Register<CommandHeader, bool>(nameof(IsButtonKind));

    private AskingDropdown? _pressed;

    /// <summary>The labels of the pressed button choice, in <see cref="TriggerKindExtensions.PressedButtons"/> order.</summary>
    public static IReadOnlyList<string> PressedButtonLabels { get; } = [.. TriggerKindExtensions.PressedButtons.Select(button => button.ToString())];

    /// <summary>Shows the pressed button choice: the trigger is a button trigger.</summary>
    public bool IsButtonKind
    {
        get => GetValue(IsButtonKindProperty);
        private set => SetValue(IsButtonKindProperty, value);
    }

    /// <summary>The button the pressed button choice shows; -1 without a button trigger.</summary>
    public int PressedButtonIndex => _pressed?.SelectedIndex ?? -1;

    /// <summary>What the pressed button choice does: asks the host for a button trigger fired by <paramref name="button"/>.</summary>
    public void ChoosePressedButton(MouseButton button)
    {
        if (Item is { Trigger: Trigger.ButtonTrigger trigger } item && trigger.Button != button)
        {
            ActionRequested?.Invoke(this, new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerButton, command: item, button: button));
        }

        ApplyHold();
    }

    private void FindButtonParts(TemplateAppliedEventArgs e)
    {
        if (e.NameScope.Find<ComboBox>("PART_TriggerButton") is { } pressed)
        {
            _pressed = new AskingDropdown(pressed, index => ChoosePressedButton(TriggerKindExtensions.PressedButtons[index]));
        }
    }

    private void ApplyPressedButton(Trigger trigger)
    {
        IsButtonKind = trigger is Trigger.ButtonTrigger;
        _pressed?.Show(PressedButtonLabels, trigger is Trigger.ButtonTrigger button ? TriggerKindExtensions.PressedButtons.ToList().IndexOf(button.Button) : -1);
    }
}
