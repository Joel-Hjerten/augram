namespace Augram.App.Components.StepList;

/// <summary>The intents a <see cref="StepList"/> raises for the host view model (F5a step editing); the event args carry the step and, where needed, the type, the edited step or the target index.</summary>
public enum StepListAction
{
    /// <summary>The user selected a step (the arrow keys, a press in its form): the keys and the menu act on it; it opens and closes nothing.</summary>
    Select,

    /// <summary>
    /// A click on a step's header: select it and open it, or close it when it is open. The other steps stay open or closed
    /// (Joel, 2026-10-12: several steps may be open at once).
    /// </summary>
    ToggleOpen,

    /// <summary>Append a default step of the chosen type, select and open it.</summary>
    Add,

    /// <summary>
    /// Replace the step with a default step of the chosen type (the menu's "Change to…"), keeping its on/off state and dropping its
    /// settings; one undo step; select and open it.
    /// </summary>
    ChangeType,

    /// <summary>An open step's form produced a new step record; one undo step per edit.</summary>
    Edit,

    /// <summary>A copy directly below the step (Ctrl+D).</summary>
    Duplicate,

    /// <summary>Copy the step to the in-memory clipboard.</summary>
    Copy,

    /// <summary>Paste the copied step at the bottom of the list (Ctrl+V).</summary>
    Paste,

    /// <summary>Remove the step; no confirmation, undo covers it.</summary>
    Delete,

    ToggleActive,

    /// <summary>Move the step to the target index (left-button drag).</summary>
    Reorder,

    Undo,

    Redo,
}
