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

    /// <summary>A new hold remap in the selected app group (F9, plan 0002; the group's menu), selected so its form shows.</summary>
    NewHoldRemap,

    /// <summary>Show or hide the groups and commands used only on the other platform (F8; the toolbar toggle).</summary>
    ToggleOtherPlatforms,

    /// <summary>Set where the command takes part (F8 "Use on"; the header's two check boxes; <see cref="CommandTreeActionEventArgs.UseOn"/>).</summary>
    SetUseOn,

    /// <summary>Drop this platform's own steps; it runs the converted original again (F8).</summary>
    UseConvertedOriginal,

    /// <summary>Mark this platform's own steps as checked against the current original, clearing the "original changed" flag (F8).</summary>
    MarkOwnVersionChecked,

    /// <summary>Rename the section or command to the name typed in place.</summary>
    Rename,

    /// <summary>Delete the section (an app group or a hold remap with its commands, a category whose commands move to Uncategorized) or the command; the host asks first.</summary>
    Delete,

    ToggleActive,

    /// <summary>Copy the command, or a hold remap with its commands, to the in-memory clipboard.</summary>
    Copy,

    /// <summary>Paste the copied command into the section (a hold remap keeps it under it), or a copied hold remap into the section's app group.</summary>
    Paste,

    /// <summary>Export the section's app group to an Augram file (plan 0003; a group's or a category's menu): the export dialog with that group, or Global, preselected.</summary>
    Export,

    /// <summary>Change the command's trigger kind (the header dropdown); Gesture opens the picker.</summary>
    SetTriggerKind,

    /// <summary>Change what the trigger holds (the header's "While holding" boxes and capture choice; <see cref="CommandTreeActionEventArgs.Hold"/>).</summary>
    SetTriggerHold,

    /// <summary>Change a wheel trigger's direction (the Up / Down choice beside the kind; <see cref="CommandTreeActionEventArgs.Wheel"/>).</summary>
    SetWheelDirection,

    /// <summary>Change the kind of a hold remap command's input (the header's Input dropdown; <see cref="CommandTreeActionEventArgs.InputKind"/>).</summary>
    SetInputKind,

    /// <summary>Set a hold remap command's input: a detected button added or a chip removed, a wheel direction, a captured key (<see cref="CommandTreeActionEventArgs.Input"/>).</summary>
    SetInput,

    /// <summary>Move the command into another category of its group (the header's Category dropdown); Uncategorized clears it.</summary>
    SetCategory,

    /// <summary>Open the Select Gesture picker for the command (the header's glyph button).</summary>
    PickGesture,

    Undo,

    Redo,
}
