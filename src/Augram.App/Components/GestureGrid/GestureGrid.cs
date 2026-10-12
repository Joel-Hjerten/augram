using Augram.App.Components.ListEditing;
using Augram.App.Components.Parts;
using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Augram.App.Components.GestureGrid;

/// <summary>
/// Lookless grid of gesture tiles (F3, F4, F5a): <see cref="Tiles"/> in (already sorted by the host),
/// one <see cref="GestureGridActionEventArgs"/> out per user intent. The template supplies
/// <c>PART_Tiles</c> (a <see cref="ListBox"/> the tiles live in), the toolbar buttons <c>PART_New</c>,
/// <c>PART_Import</c>, <c>PART_Export</c>, <c>PART_Undo</c>, <c>PART_Redo</c>, and binds <see cref="Message"/> (rule
/// feedback). Selecting a tile marks
/// its confusion partners with their score. Selection survives a rebuild by gesture id. Right-click selects before the context menu opens; double-click redraws.
/// </summary>
public sealed class GestureGrid : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<GestureTileItem>> TilesProperty =
        AvaloniaProperty.Register<GestureGrid, IReadOnlyList<GestureTileItem>>(nameof(Tiles), []);

    public static readonly StyledProperty<IReadOnlyList<GestureTile>> TileControlsProperty =
        AvaloniaProperty.Register<GestureGrid, IReadOnlyList<GestureTile>>(nameof(TileControls), []);

    public static readonly StyledProperty<bool> CanUndoProperty =
        AvaloniaProperty.Register<GestureGrid, bool>(nameof(CanUndo));

    public static readonly StyledProperty<bool> CanRedoProperty =
        AvaloniaProperty.Register<GestureGrid, bool>(nameof(CanRedo));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<GestureGrid, string?>(nameof(Message));

    private readonly RowListHost _rows;
    private GestureId? _selectedId;

    public GestureGrid()
    {
        _rows = new RowListHost(() => IsEditing);
    }

    public event EventHandler<GestureGridActionEventArgs>? ActionRequested;

    public IReadOnlyList<GestureTileItem> Tiles
    {
        get => GetValue(TilesProperty);
        set => SetValue(TilesProperty, value);
    }

    /// <summary>One <see cref="GestureTile"/> per item, in order; the template's list shows these.</summary>
    public IReadOnlyList<GestureTile> TileControls
    {
        get => GetValue(TileControlsProperty);
        private set => SetValue(TileControlsProperty, value);
    }

    public bool CanUndo
    {
        get => GetValue(CanUndoProperty);
        set => SetValue(CanUndoProperty, value);
    }

    public bool CanRedo
    {
        get => GetValue(CanRedoProperty);
        set => SetValue(CanRedoProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public GestureTileItem? SelectedTile => (_rows.SelectedItem as GestureTile)?.Item;

    /// <summary>True while a tile's name is being edited in place; the list's key bindings stand down then.</summary>
    public bool IsEditing => RowListHost.AnyEditing(TileControls);

    /// <summary>Selects the gesture's tile as a click would (its partners light up); remembered across a rebuild and a template.</summary>
    public void Select(GestureId id)
    {
        _selectedId = id;
        if (_rows.List is not null)
        {
            _rows.Select(TileControls.FirstOrDefault(tile => tile.Item?.Id == id));
        }
    }

    /// <summary>Starts in-place rename of the selected tile (the rename keys of <see cref="EditKeymap"/> or the menu).</summary>
    public void BeginRename() => _rows.RenameSelected();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // The grid never applies a selection quietly: every change, the rebuild's included, remembers the id and marks partners.
        // A double click redraws (DoubleTapped: a tile has no press-count rule).
        if (_rows.Attach(e.NameScope.Find<ListBox>("PART_Tiles"), OnSelectionChanged, () => Request(GestureGridAction.Redraw)) is { } list)
        {
            list.ContextMenu = GestureGridMenu.Build(Request);
            list.ContextMenu.Opening += (_, _) => GestureGridMenu.Refresh(list.ContextMenu, SelectedTile);
            // On the grid, not the list: key bindings are evaluated from the focused element upwards,
            // so they work from a focused tile and from the toolbar buttons alike.
            KeyBindings.Clear();
            GestureGridMenu.BindKeys(this, EditKeymap.Current, Request, _rows.KeysEnabled);
            if (_selectedId is { } selected)
            {
                Select(selected);
            }
        }

        e.OnClick("PART_New", () => Request(GestureGridAction.New));
        e.OnClick("PART_Import", () => Request(GestureGridAction.Import));
        e.OnClick("PART_Export", () => Request(GestureGridAction.Export));
        e.OnClick("PART_Undo", () => Request(GestureGridAction.Undo));
        e.OnClick("PART_Redo", () => Request(GestureGridAction.Redo));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TilesProperty)
        {
            Rebuild();
        }
    }


    private void Rebuild()
    {
        var tiles = new List<GestureTile>(Tiles.Count);
        foreach (var item in Tiles)
        {
            var tile = new GestureTile { Item = item };
            tile.RenameCommitted += (_, name) => Raise(GestureGridAction.Rename, item, name);
            tile.PointerPressed += OnTilePressed;
            tiles.Add(tile);
        }

        TileControls = tiles;
        if (_selectedId is { } id)
        {
            Select(id);
        }

        MarkPartners();
    }

    /// <summary>Lights up the selected gesture's duplicates (A7) with the score.</summary>
    private void MarkPartners()
    {
        var partners = SelectedTile?.Partners ?? [];
        foreach (var tile in TileControls)
        {
            var match = tile.Item is { } item ? partners.FirstOrDefault(partner => partner.Id == item.Id) : null;
            tile.SetPartner(match?.Score);
        }
    }

    private void OnSelectionChanged()
    {
        _selectedId = SelectedTile?.Id ?? _selectedId;
        MarkPartners();
    }

    private void OnTilePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is GestureTile tile)
        {
            _rows.SelectOnRightPress(tile, e);
        }
    }

    private void Request(GestureGridAction action)
    {
        var tile = SelectedTile;
        switch (action)
        {
            case GestureGridAction.Rename:
                BeginRename();
                return;
            case GestureGridAction.Redraw or GestureGridAction.Delete or GestureGridAction.KeepThis or GestureGridAction.UsedBy or GestureGridAction.CleanUp or GestureGridAction.RestoreOriginal when tile is null:
                return;
            default:
                Raise(action, tile, null);
                return;
        }
    }

    private void Raise(GestureGridAction action, GestureTileItem? tile, string? name)
        => ActionRequested?.Invoke(this, new GestureGridActionEventArgs(action, tile, name));
}
