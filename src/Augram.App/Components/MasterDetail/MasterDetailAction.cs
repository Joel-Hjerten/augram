namespace Augram.App.Components.MasterDetail;

/// <summary>What the user asked of a <see cref="MasterDetail"/>; the host turns each into a store call.</summary>
public enum MasterDetailAction
{
    /// <summary>A row was selected (or the selection cleared): the host shows its form.</summary>
    Select,

    /// <summary>The new button, the menu entry or the new key: the host asks for the new item.</summary>
    New,

    /// <summary>An in-place rename was committed; the event carries the name.</summary>
    Rename,

    /// <summary>Delete the selected item; the host asks first.</summary>
    Delete,

    /// <summary>The row's active box was clicked.</summary>
    ToggleActive,

    Undo,

    Redo,
}
