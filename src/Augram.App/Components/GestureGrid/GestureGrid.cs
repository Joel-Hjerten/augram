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
/// <c>PART_Import</c>, <c>PART_Undo</c>, <c>PART_Redo</c>, and binds <see cref="Message"/> (rule
/// feedback) and <see cref="Diagnostic"/> (the A7 "likely to be confused" line). Selection survives a
/// rebuild by gesture id. Right-click selects before the context menu opens; double-click redraws.
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

    public static readonly StyledProperty<string?> DiagnosticProperty =
        AvaloniaProperty.Register<GestureGrid, string?>(nameof(Diagnostic));

    private ListBox? _list;
    private GestureId? _selectedId;

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

    public string? Diagnostic
    {
        get => GetValue(DiagnosticProperty);
        set => SetValue(DiagnosticProperty, value);
    }

    public GestureTileItem? SelectedTile => (_list?.SelectedItem as GestureTile)?.Item;

    /// <summary>True while a tile's name is being edited in place; the list's key bindings stand down then.</summary>
    public bool IsEditing => TileControls.Any(tile => tile.IsEditing);

    public void Select(GestureId id)
    {
        _selectedId = id;
        if (_list is not null)
        {
            _list.SelectedItem = TileControls.FirstOrDefault(tile => tile.Item?.Id == id);
        }
    }

    /// <summary>Starts in-place rename of the selected tile (F2/Return or the menu).</summary>
    public void BeginRename() => (_list?.SelectedItem as GestureTile)?.BeginEdit();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list = e.NameScope.Find<ListBox>("PART_Tiles");
        if (_list is not null)
        {
            _list.ContextMenu = GestureGridMenu.Build(Request);
            // On the grid, not the list: key bindings are evaluated from the focused element upwards,
            // so they work from a focused tile and from the toolbar buttons alike.
            KeyBindings.Clear();
            GestureGridMenu.BindKeys(this, Request, () => !IsEditing);
            _list.SelectionChanged += (_, _) => _selectedId = SelectedTile?.Id ?? _selectedId;
            _list.DoubleTapped += (_, _) => Request(GestureGridAction.Redraw);
            if (_selectedId is { } selected)
            {
                Select(selected);
            }
        }

        Wire(e, "PART_New", GestureGridAction.New);
        Wire(e, "PART_Import", GestureGridAction.Import);
        Wire(e, "PART_Undo", GestureGridAction.Undo);
        Wire(e, "PART_Redo", GestureGridAction.Redo);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TilesProperty)
        {
            Rebuild();
        }
    }

    private void Wire(TemplateAppliedEventArgs e, string part, GestureGridAction action)
    {
        if (e.NameScope.Find<Button>(part) is { } button)
        {
            button.Click += (_, _) => Request(action);
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
    }

    private void OnTilePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is GestureTile tile && _list is not null && e.GetCurrentPoint(tile).Properties.IsRightButtonPressed)
        {
            _list.SelectedItem = tile;
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
            case GestureGridAction.Redraw or GestureGridAction.Delete when tile is null:
                return;
            default:
                Raise(action, tile, null);
                return;
        }
    }

    private void Raise(GestureGridAction action, GestureTileItem? tile, string? name)
        => ActionRequested?.Invoke(this, new GestureGridActionEventArgs(action, tile, name));
}
