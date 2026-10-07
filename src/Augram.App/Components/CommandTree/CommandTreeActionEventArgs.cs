namespace Augram.App.Components.CommandTree;

/// <summary>One <see cref="CommandTreeAction"/> with the group and command it applies to, the new name for Rename, and the kind for SetTriggerKind.</summary>
public sealed class CommandTreeActionEventArgs : EventArgs
{
    public CommandTreeActionEventArgs(CommandTreeAction action, GroupItem? group = null, CommandItem? command = null, string? name = null, TriggerKind? kind = null)
    {
        Action = action;
        Group = group;
        Command = command;
        Name = name;
        Kind = kind;
    }

    public CommandTreeAction Action { get; }

    /// <summary>The group acted on, or the group of <see cref="Command"/>; null for actions without a selection.</summary>
    public GroupItem? Group { get; }

    public CommandItem? Command { get; }

    public string? Name { get; }

    public TriggerKind? Kind { get; }
}
