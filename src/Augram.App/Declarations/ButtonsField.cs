using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>
/// Buttons on one form line, with what goes beside them: Options › Sync's "Export…" and "Import…" (buttons alone, side by side),
/// "Sync now" with its status line (<see cref="Status"/>: a note that wraps), Options › General's "Detect…" with its hint
/// (<see cref="Hint"/>: the secondary text role), and the import review's dropdown before "Apply to all" (<see cref="Choice"/>).
/// One of <see cref="Status"/> and <see cref="Hint"/> at most; <see cref="Choice"/> goes with neither.
/// </summary>
public sealed record ButtonsField(
    string Label,
    IReadOnlyList<FieldButton> Buttons,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Buttons";

    public override IValueBinding? Binding => Status ?? Hint;

    /// <summary>A status line after the buttons that wraps ("Synced 2 minutes ago"); the buttons keep the theme's gap before it.</summary>
    public IValueBinding<string>? Status { get; init; }

    /// <summary>A hint right after the button, in the secondary text role ("Press a button…").</summary>
    public IValueBinding<string>? Hint { get; init; }

    /// <summary>A dropdown before the buttons; their clicks read what it holds.</summary>
    public IChoiceField? Choice { get; init; }
}
