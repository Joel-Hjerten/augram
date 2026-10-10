namespace Augram.App.Declarations;

/// <summary>One check box of a <see cref="CheckListField"/>: its caption, its own bool binding, and a muted detail after the caption ("Windows only", "8 commands"), or none.</summary>
public sealed record CheckListItem(string Caption, IValueBinding<bool> Value, string? Detail = null);
