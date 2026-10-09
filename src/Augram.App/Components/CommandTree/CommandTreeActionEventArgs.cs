using Augram.Core.Capture;
using Augram.Core.Mapping;
namespace Augram.App.Components.CommandTree;

/// <summary>
/// One <see cref="CommandTreeAction"/> with the section and command it applies to, the new name for
/// Rename, the kind for SetTriggerKind, the chosen category for SetCategory, the set for SetTriggerHold and the
/// direction for SetWheelDirection.
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
        WheelDirection? wheel = null)
    {
        Hold = hold;
        Wheel = wheel;
        UseOn = useOn;
        Action = action;
        Section = section;
        Command = command;
        Name = name;
        Kind = kind;
        Category = category;
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
}
