namespace Augram.App.Components.CommandTree;

/// <summary>
/// The intents a <see cref="CommandTree"/> (and the <see cref="CommandHeader"/>) raises for the host
/// view model (F5a list editing). The event args say which group and command they apply to: an
/// action with a command set acts on the command, one with only a group set acts on the group.
/// </summary>
public enum CommandTreeAction
{
    /// <summary>The user selected a group row (command null) or a command row.</summary>
    Select,

    /// <summary>Show or hide the group's commands.</summary>
    ToggleExpanded,

    /// <summary>Open the app group form for a new group.</summary>
    NewGroup,

    /// <summary>Reopen the app group form for the group ("Edit app definition…").</summary>
    EditGroup,

    /// <summary>A "New command N" in the group (the selected one, Global when none), selected and ready to rename.</summary>
    NewCommand,

    /// <summary>Rename the group or command to the name typed in place.</summary>
    Rename,

    /// <summary>Delete the group (with its commands) or the command; the host asks first.</summary>
    Delete,

    ToggleActive,

    /// <summary>Copy the command to the in-memory clipboard.</summary>
    Copy,

    /// <summary>Paste the copied command into the group.</summary>
    Paste,

    /// <summary>Change the command's trigger kind (the header dropdown); Gesture opens the picker.</summary>
    SetTriggerKind,

    /// <summary>Open the Select Gesture picker for the command (the header's glyph button).</summary>
    PickGesture,

    Undo,

    Redo,
}
