using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>Read-only text beside a label: explanations, and live values such as the health summary.</summary>
public sealed record NoteField(
    string Label,
    IValueBinding<string> Text,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public NoteField(string label, string text, string? help = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        : this(label, new DelegateBinding<string>(() => text, propertyName: null), help, file, line)
    {
    }

    public override string Kind => "Note";

    public override IValueBinding Binding => Text;
}
