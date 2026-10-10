using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
namespace Augram.App.Components.CommandTree;

/// <summary>
/// One <see cref="CommandTreeAction"/> with the section and command it applies to, the new name for
/// Rename, the kind for SetTriggerKind, the chosen category for SetCategory, the set for SetTriggerHold, the
/// direction for SetWheelDirection, the pressed button for SetTriggerButton, and for a command under a hold remap the kind for
/// SetInputKind and the input for SetInput.
/// </summary>
public sealed class CommandTreeActionEventArgs : EventArgs
{
    public CommandTreeActionEventArgs(
        CommandTreeAction action,
        SectionItem? section = null,
        CommandItem? command = null,
        string? name = null,
        TriggerKind? kind = null,
        CategoryChoice? category = null,
        PlatformSet? useOn = null,
        TriggerHold? hold = null,
        WheelDirection? wheel = null,
        InputKind? inputKind = null,
        HoldInput? input = null,
        MouseButton? button = null)
    {
        Hold = hold;
        Wheel = wheel;
        Button = button;
        UseOn = useOn;
        Action = action;
        Section = section;
        Command = command;
        Name = name;
        Kind = kind;
        Category = category;
        InputKind = inputKind;
        Input = input;
    }

    public CommandTreeAction Action { get; }

    /// <summary>The section acted on, or the section of <see cref="Command"/>; null for actions without a selection.</summary>
    public SectionItem? Section { get; }

    public CommandItem? Command { get; }

    public string? Name { get; }

    public TriggerKind? Kind { get; }

    public CategoryChoice? Category { get; }

    /// <summary>The platforms a <see cref="CommandTreeAction.SetUseOn"/> asks for.</summary>
    public PlatformSet? UseOn { get; }

    /// <summary>The "while holding" set a <see cref="CommandTreeAction.SetTriggerHold"/> asks for.</summary>
    public TriggerHold? Hold { get; }

    /// <summary>The direction a <see cref="CommandTreeAction.SetWheelDirection"/> asks for.</summary>
    public WheelDirection? Wheel { get; }

    /// <summary>The pressed button a <see cref="CommandTreeAction.SetTriggerButton"/> asks for (plan 0005).</summary>
    public MouseButton? Button { get; }

    /// <summary>The input kind a <see cref="CommandTreeAction.SetInputKind"/> asks for.</summary>
    public InputKind? InputKind { get; }

    /// <summary>The input a <see cref="CommandTreeAction.SetInput"/> asks for (a button set may be empty, a key unset, while it is composed).</summary>
    public HoldInput? Input { get; }
}
