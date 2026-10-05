using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

public sealed record ToggleField(
    string Label,
    IValueBinding<bool> Value,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Toggle";

    public override IValueBinding Binding => Value;
}
