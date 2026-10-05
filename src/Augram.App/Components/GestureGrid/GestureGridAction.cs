namespace Augram.App.Components.GestureGrid;

/// <summary>The intents a <see cref="GestureGrid"/> raises (F5a list editing, F8 import); the host view model acts on them.</summary>
public enum GestureGridAction
{
    /// <summary>Open the training popup for a new gesture.</summary>
    New,

    /// <summary>Open the training popup to add a sample to the selected gesture.</summary>
    AddSample,

    /// <summary>Rename the selected gesture to the name the user typed in place.</summary>
    Rename,

    /// <summary>Flip the selected gesture's active flag.</summary>
    ToggleActive,

    /// <summary>Delete the selected gesture (no confirmation; undo covers it, A9).</summary>
    Delete,

    /// <summary>Start the StrokesPlus.net import flow.</summary>
    Import,

    Undo,

    Redo,
}
