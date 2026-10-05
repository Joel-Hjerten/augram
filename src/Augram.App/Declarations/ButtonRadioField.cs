using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>A row of mutually exclusive buttons: for few choices that should all be visible (stroke button).</summary>
public sealed record ButtonRadioField<T>(
    string Label,
    IReadOnlyList<Choice<T>> Choices,
    IValueBinding<T> Value,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line), IChoiceSource
{
    public override string Kind => "ButtonRadio";

    public override IValueBinding Binding => Value;

    public IChoiceField AsChoices() => new ChoiceBinding<T>(Choices, Value);
}
