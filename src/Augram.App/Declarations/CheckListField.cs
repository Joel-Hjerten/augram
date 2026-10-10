using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>
/// A list of check boxes one under the other, each with its own binding and an optional detail beside its caption: the
/// export dialog's "Global · Blender (Windows only) · Steam" selection (plan 0003). For a few bools on one row,
/// <see cref="TogglesField"/>. The list scrolls past a theme height. <see cref="Binding"/> is the first item's, for the inspector.
/// </summary>
public sealed record CheckListField(
    string Label,
    IReadOnlyList<CheckListItem> Items,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "CheckList";

    public override IValueBinding? Binding => Items.Count > 0 ? Items[0].Value : null;
}
