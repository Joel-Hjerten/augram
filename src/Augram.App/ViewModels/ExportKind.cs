namespace Augram.App.ViewModels;

/// <summary>The export dialog's scope choice (plan 0003, decision 2's three scopes); <see cref="ExportViewModel"/> turns it into a Core <c>ExportScope</c>.</summary>
public enum ExportKind
{
    /// <summary>The options (without Sync), every gesture, the whole mapping.</summary>
    Everything,

    /// <summary>The gesture library alone.</summary>
    GesturesOnly,

    /// <summary>The app groups (Global among them) and ignored apps ticked in the list, with the gestures their commands use.</summary>
    Selected,
}
