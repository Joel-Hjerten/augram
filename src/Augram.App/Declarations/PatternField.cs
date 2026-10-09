using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Augram.App.Declarations;

/// <summary>
/// One matching field as StrokesPlus.net's App Definition shows it (Joel, 2026-10-09): the text box, the magnifier after it
/// (<see cref="Finder"/>; none where this machine cannot pick a value for the field), and a "Use Regex" check box, on one
/// row. <see cref="Placeholder"/> is grey text shown while the box is empty (the known-app guess for an executable).
/// </summary>
public sealed record PatternField(
    string Label,
    IValueBinding<string> Value,
    IValueBinding<bool> IsRegex,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public const string UseRegexCaption = "Use Regex";

    public override string Kind => "Pattern";

    public override IValueBinding Binding => Value;

    public override bool StretchesEditor => true;

    public Func<Control>? Finder { get; init; }

    /// <summary>Whether the magnifier shows now (only on the side of the form for this machine's platform); always when null.</summary>
    public IValueBinding<bool>? FinderVisible { get; init; }

    public IValueBinding<string>? Placeholder { get; init; }
}
