using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

public sealed record DropdownField<T>(
    string Label,
    IReadOnlyList<Choice<T>> Choices,
    IValueBinding<T> Value,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line), IChoiceSource
{
    public override string Kind => "Dropdown";

    public override IValueBinding Binding => Value;

    public IChoiceField AsChoices() => new ChoiceBinding<T>(Choices, Value);
}
