using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>
/// One row of several captioned check boxes side by side, each with its own binding: "Use on ☑ Windows ☑ macOS"
/// (Joel, 2026-10-07: one row, not a row per platform). <see cref="Binding"/> is the first option's, for the inspector.
/// </summary>
public sealed record TogglesField(
    string Label,
    IReadOnlyList<ToggleOption> Options,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Toggles";

    public override IValueBinding? Binding => Options.Count > 0 ? Options[0].Value : null;
}
