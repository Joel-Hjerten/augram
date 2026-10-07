namespace Augram.App.Components.GestureGrid;

/// <summary>The intents a <see cref="GestureGrid"/> raises (F5a list editing, F8 import); the host view model acts on them.</summary>
public enum GestureGridAction
{
    /// <summary>Open the training popup for a new gesture.</summary>
    New,

    /// <summary>Open the training popup to redraw the selected gesture; Accept replaces its sample (F3, no averaging).</summary>
    Redraw,

    /// <summary>Rename the selected gesture to the name the user typed in place.</summary>
    Rename,

    /// <summary>Delete the selected gesture (no confirmation; undo covers it, A9).</summary>
    Delete,

    /// <summary>Keep the selected gesture and delete its exact duplicates (A7), one undo step. M2 also retargets the commands that used them.</summary>
    KeepThis,

    /// <summary>Start the StrokesPlus.net import flow (toolbar button).</summary>
    Import,

    Undo,

    Redo,
}
