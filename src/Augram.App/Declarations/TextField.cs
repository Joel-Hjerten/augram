using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

public sealed record TextField(
    string Label,
    IValueBinding<string> Value,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Text";

    public override IValueBinding Binding => Value;
}
