using Augram.App.Components.GestureGrid;
using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.GesturePicker;

/// <summary>
/// Lookless content of the Select Gesture picker (F3): every gesture as a <see cref="GestureTile"/>
/// (the Gestures tab's tile, without its grid's toolbar) in <c>PART_Tiles</c>, single selection, and
/// the buttons <c>PART_NoGesture</c>, <c>PART_NewGesture</c>, <c>PART_Ok</c>, <c>PART_Cancel</c>.
/// Every way out raises <see cref="Closed"/> with a <see cref="GesturePickerResult"/>; New Gesture…
/// raises <see cref="NewGestureRequested"/> for the host to open training, and the host selects the
/// accepted gesture through <see cref="Select"/>. Selection survives a tile rebuild by id.
/// </summary>
public sealed class GesturePicker : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<GestureTileItem>> TilesProperty =
        AvaloniaProperty.Register<GesturePicker, IReadOnlyList<GestureTileItem>>(nameof(Tiles), []);

    public static readonly StyledProperty<IReadOnlyList<GestureTile>> TileControlsProperty =
        AvaloniaProperty.Register<GesturePicker, IReadOnlyList<GestureTile>>(nameof(TileControls), []);

    public static readonly StyledProperty<bool> HasSelectionProperty =
        AvaloniaProperty.Register<GesturePicker, bool>(nameof(HasSelection));

    private ListBox? _list;
    private GestureId? _selectedId;

    public event EventHandler<GesturePickerResult>? Closed;

    public event EventHandler? NewGestureRequested;

    public IReadOnlyList<GestureTileItem> Tiles
    {
        get => GetValue(TilesProperty);
        set => SetValue(TilesProperty, value);
    }

    public IReadOnlyList<GestureTile> TileControls
    {
        get => GetValue(TileControlsProperty);
        private set => SetValue(TileControlsProperty, value);
    }

    /// <summary>True while a tile is selected; OK is enabled then.</summary>
    public bool HasSelection
    {
        get => GetValue(HasSelectionProperty);
        private set => SetValue(HasSelectionProperty, value);
    }

    public GestureId? SelectedId => (_list?.SelectedItem as GestureTile)?.Item?.Id ?? _selectedId;

    public void Select(GestureId? id)
    {
        _selectedId = id;
        if (_list is not null)
        {
            _list.SelectedItem = id is { } wanted ? TileControls.FirstOrDefault(tile => tile.Item?.Id == wanted) : null;
        }

        HasSelection = SelectedId is not null;
    }

    /// <summary>What OK does: closes with the selected gesture; nothing without one.</summary>
    public void Accept()
    {
        if (SelectedId is { } id)
        {
            Closed?.Invoke(this, GesturePickerResult.Selected(id));
        }
    }

    public void Cancel() => Closed?.Invoke(this, GesturePickerResult.Cancelled);

    public void ChooseNoGesture() => Closed?.Invoke(this, GesturePickerResult.NoGesture);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list = e.NameScope.Find<ListBox>("PART_Tiles");
        if (_list is not null)
        {
            _list.SelectionChanged += (_, _) =>
            {
                _selectedId = (_list.SelectedItem as GestureTile)?.Item?.Id ?? _selectedId;
                HasSelection = _list.SelectedItem is not null;
            };
            _list.DoubleTapped += (_, _) => Accept();
            Select(_selectedId);
        }

        Wire(e, "PART_Ok", Accept);
        Wire(e, "PART_Cancel", Cancel);
        Wire(e, "PART_NoGesture", ChooseNoGesture);
        Wire(e, "PART_NewGesture", () => NewGestureRequested?.Invoke(this, EventArgs.Empty));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TilesProperty)
        {
            TileControls = [.. Tiles.Select(item => new GestureTile { Item = item })];
            Select(_selectedId);
        }
    }

    private static void Wire(TemplateAppliedEventArgs e, string part, Action action)
    {
        if (e.NameScope.Find<Button>(part) is { } button)
        {
            button.Click += (_, _) => action();
        }
    }
}
