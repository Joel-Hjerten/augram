using Augram.Core.Mapping;
namespace Augram.App.Components.CommandTree;

/// <summary>
/// One <see cref="CommandTreeAction"/> with the section and command it applies to, the new name for
/// Rename, the kind for SetTriggerKind and the chosen category for SetCategory.
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
        PlatformSet? useOn = null)
    {
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
}
