using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Augram.App.Declarations;

/// <summary>
/// A special component inside an otherwise declarative screen (ADR-0002 §5c). <see cref="Build"/>
/// produces the control; <see cref="ViewModel"/> becomes its DataContext so the component stays
/// presentational and the screen's view model owns the logic.
/// </summary>
public sealed record CustomField(
    string Label,
    Func<Control> Build,
    object? ViewModel = null,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Custom";

    public override IValueBinding? Binding => null;
}
