namespace Augram.App.Components.MasterDetail;

/// <summary>One <see cref="MasterDetailAction"/> with the item it applies to (null for New, Undo, Redo and a cleared selection) and the new name for Rename.</summary>
public sealed class MasterDetailActionEventArgs(MasterDetailAction action, MasterItem? item = null, string? name = null) : EventArgs
{
    public MasterDetailAction Action { get; } = action;

    public MasterItem? Item { get; } = item;

    public string? Name { get; } = name;
}
