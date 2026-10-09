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

    /// <summary>Delete the selected gesture. No confirmation while no command uses it (undo covers it, A9); a used gesture asks first and unbinds its commands.</summary>
    Delete,

    /// <summary>Keep the selected gesture and delete its exact duplicates (A7), one undo step in the library; the commands that used the duplicates are retargeted to the kept gesture.</summary>
    KeepThis,

    /// <summary>Open the "Used by…" popup: every app group › command bound to the selected gesture (F3).</summary>
    UsedBy,

    /// <summary>Replace the selected gesture's samples with their cleaned shape, keeping the drawn ones (plan 0001 M2 step 10); one undo step.</summary>
    CleanUp,

    /// <summary>Put the selected gesture's drawn samples back in place of the cleaned shape; one undo step.</summary>
    RestoreOriginal,

    /// <summary>Start the StrokesPlus.net import flow (toolbar button).</summary>
    Import,

    Undo,

    Redo,
}
