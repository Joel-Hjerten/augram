namespace Augram.App.Components.StepList;

/// <summary>The intents a <see cref="StepList"/> raises for the host view model (F5a step editing); the event args carry the step and, where needed, the type, the edited step or the target index.</summary>
public enum StepListAction
{
    /// <summary>The user selected a step; the host expands it in place.</summary>
    Select,

    /// <summary>Append a default step of the chosen type, select and expand it.</summary>
    Add,

    /// <summary>The expanded step's form produced a new step record; one undo step per edit.</summary>
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
