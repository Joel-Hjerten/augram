using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>A numeric field with a range. Integers bind as doubles; the view model rounds.</summary>
public sealed record NumberField(
    string Label,
    IValueBinding<double> Value,
    double Min,
    double Max,
    double Step = 1,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Number";

    public override IValueBinding Binding => Value;
}
