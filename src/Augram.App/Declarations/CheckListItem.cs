namespace Augram.App.Declarations;

/// <summary>
/// One check box of a <see cref="CheckListField"/>: its caption, its own bool binding, and a muted detail after the caption
/// ("Windows only", "8 commands"), or none. <see cref="Remove"/>, when set, lets the item read as removable and be removed
/// (the Not in and Also in dialogs' unused apps); the export and Allowed for dialogs leave it null.
/// </summary>
public sealed record CheckListItem(string Caption, IValueBinding<bool> Value, string? Detail = null)
{
    public CheckListRemove? Remove { get; init; }
}
