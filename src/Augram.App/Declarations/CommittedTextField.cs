using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>
/// A text box committed on leaving it or Enter, reverted on Escape, with the draft's problem under it: Options › Sync's
/// repository address and machine name (over a <c>ViewModels/DraftText</c>). Every keystroke is set on <see cref="Text"/> as a
/// draft; nothing is stored until <see cref="Commit"/>. For a value stored on every keystroke, <see cref="TextField"/>.
/// </summary>
public sealed record CommittedTextField(
    string Label,
    IValueBinding<string> Text,
    IValueBinding<string> Problem,
    Action Commit,
    Action Revert,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "CommittedText";

    public override IValueBinding Binding => Text;

    /// <summary>The wide text box (a URL), the theme's <c>wide</c> class.</summary>
    public bool Wide { get; init; }
}
