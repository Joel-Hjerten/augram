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

    /// <summary>
    /// Several lines (a typed-text step): Enter adds a line break, written as LF on every platform so a value reads the
    /// same on each; long lines wrap; the editor grows with its text up to the theme's maximum height, then scrolls.
    /// </summary>
    public bool Multiline { get; init; }
}
