namespace Augram.App.Components.CommandTree;

/// <summary>
/// The intents a <see cref="CommandTree"/> (and the <see cref="CommandHeader"/>) raises for the host
/// view model (F5a list editing). The event args say which section and command they apply to: an
/// action with a command set acts on the command, one with only a section set acts on the section.
/// A section is an app group on the Apps tab and a category (or Uncategorized) on the Global tab; what
/// a section's header offers is on its <see cref="SectionItem"/>.
/// </summary>
public enum CommandTreeAction
{
    /// <summary>The user selected a section row (command null) or a command row.</summary>
    Select,

    /// <summary>Show or hide the section's commands.</summary>
    ToggleExpanded,

    /// <summary>
    /// A new section, labelled by the host (<see cref="CommandTree.NewSectionLabel"/>): the app group form
    /// on the Apps tab, a "New category N" ready to rename on the Global tab.
    /// </summary>
    NewSection,

    /// <summary>A "New command N" in the selected section, selected and ready to rename.</summary>
    NewCommand,

    /// <summary>Show or hide the groups used only on the other platform (F8; the toolbar toggle, Apps tab).</summary>
    ToggleOtherPlatforms,

    /// <summary>Rename the section or command to the name typed in place.</summary>
    Rename,

    /// <summary>Delete the section (an app group with its commands, a category whose commands move to Uncategorized) or the command; the host asks first.</summary>
    Delete,

    ToggleActive,

    /// <summary>Copy the command to the in-memory clipboard.</summary>
    Copy,

    /// <summary>Paste the copied command into the section.</summary>
    Paste,

    /// <summary>Change the command's trigger kind (the header dropdown); Gesture opens the picker.</summary>
    SetTriggerKind,

    /// <summary>Move the command into another category of its group (the header's Category dropdown); Uncategorized clears it.</summary>
    SetCategory,

    /// <summary>Open the Select Gesture picker for the command (the header's glyph button).</summary>
    PickGesture,

    Undo,

    Redo,
}
