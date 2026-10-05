namespace Augram.App.Components.GestureGrid;

/// <summary>One <see cref="GestureGridAction"/> with the tile it applies to (null for New, Import, Undo, Redo) and, for Rename, the new name.</summary>
public sealed class GestureGridActionEventArgs : EventArgs
{
    public GestureGridActionEventArgs(GestureGridAction action, GestureTileItem? tile, string? name = null)
    {
        Action = action;
        Tile = tile;
        Name = name;
    }

    public GestureGridAction Action { get; }

    public GestureTileItem? Tile { get; }

    public string? Name { get; }
}
