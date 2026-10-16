namespace Augram.App.Components.HeldInputList;

/// <summary>The intents a <see cref="HeldInputList"/> raises for its host (plan 0012); the event args carry the row, the input, the target index or the platform where needed.</summary>
public enum HeldInputListAction
{
    /// <summary>The user selected a row (a click, the arrow keys): the keys and the menu act on it.</summary>
    Select,

    /// <summary>Add the recorded or picked input (<see cref="HeldInputListActionEventArgs.Input"/>) at the bottom.</summary>
    Add,

    /// <summary>Copy the row's input to the in-memory clipboard.</summary>
    Copy,

    /// <summary>Paste the copied input at the bottom.</summary>
    Paste,

    /// <summary>Remove the row; no confirmation, undo covers it.</summary>
    Delete,

    /// <summary>Move the row to the target index (left-button drag).</summary>
    Reorder,

    /// <summary>A row of the other platform's side: add its input, converted, to this platform's side (plan 0012 decision 6).</summary>
    CopyTo,

    /// <summary>The "Ignore for" switch: show the platform's side (<see cref="HeldInputListActionEventArgs.Platform"/>); a view change only.</summary>
    ShowSide,

    /// <summary>"Copy from Windows / macOS": the side in view takes the other platform's whole list.</summary>
    CopyFromOtherPlatform,

    Undo,

    Redo,
}
