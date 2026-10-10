using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// What the command header's Input dropdown offers for a command under a hold remap (F9, plan 0002): a button or buttons held
/// together, a wheel direction, a key, or no input yet. The closed set behind <see cref="HoldInput"/>, as the UI names it.
/// </summary>
public enum InputKind
{
    Buttons,
    Wheel,
    Key,
    None,
}

/// <summary>The words for <see cref="InputKind"/> and for an input in the header and the row: "Left + Right", "wheel up", "W", "No input".</summary>
public static class InputKindExtensions
{
    public const string NoInputText = "No input";

    /// <summary>The dropdown's entries, in this order.</summary>
    public static IReadOnlyList<InputKind> All { get; } = [InputKind.Buttons, InputKind.Wheel, InputKind.Key, InputKind.None];

    public static string Label(this InputKind kind) => kind switch
    {
        InputKind.Buttons => "Buttons",
        InputKind.Wheel => "Wheel",
        InputKind.Key => "Key",
        _ => NoInputText,
    };

    public static InputKind KindOf(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger is Trigger.InputTrigger { Input: var input }
            ? input switch
            {
                HoldInput.Buttons => InputKind.Buttons,
                HoldInput.Wheel => InputKind.Wheel,
                _ => InputKind.Key,
            }
            : InputKind.None;
    }

    /// <summary>"Left + Right", "wheel up", "W" (<see cref="HoldInput.Describe"/>); "No input" for none, or a set or key not chosen yet.</summary>
    public static string Text(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger is Trigger.InputTrigger { Input: not HoldInput.Key { KeyCode: KeyCode.None } and var input } && input.Describe() is { Length: > 0 } text
            ? text
            : NoInputText;
    }

    /// <summary>The line under the input: when it fires, or that the hold remap has no hold key yet.</summary>
    public static string Hint(HoldRemap holdRemap)
    {
        ArgumentNullException.ThrowIfNull(holdRemap);
        return holdRemap.HoldKey == KeyCode.None
            ? $"'{holdRemap.Name}' has no hold key yet: select it in the list and choose one."
            : $"Fires while {HotkeyText.KeyName(holdRemap.HoldKey)} is held.";
    }
}
