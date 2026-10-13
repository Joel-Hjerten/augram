using Augram.App.Components.GestureGrid;
using Augram.App.Components.ListEditing;
using Augram.App.Components.Parts;
using Augram.App.Components.SearchBox;
using Augram.App.Dialogs;
using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Augram.App.Components.GesturePicker;

/// <summary>
/// Lookless content of the Select Gesture picker (F3): every gesture as a <see cref="GestureTile"/>
/// (the Gestures tab's tile, without its grid's toolbar) in <c>PART_Tiles</c>, single selection, in the dialog shell
/// <c>PART_Shell</c> (<see cref="DialogShell"/>: OK and Cancel) with the buttons <c>PART_NoGesture</c> and <c>PART_NewGesture</c>
/// before them.
/// Every way out raises <see cref="Closed"/> with a <see cref="GesturePickerResult"/>; New Gesture…
/// raises <see cref="NewGestureRequested"/> for the host to open training, and the host selects the
/// accepted gesture through <see cref="Select"/>. Selection survives a tile rebuild by id. The search box on the heading row, right
/// (<c>PART_Search</c> in the shell's <c>HeadingTrailing</c>, plan 0009) is two-way bound to <see cref="SearchText"/> in code, since a TemplateBinding inside the slot would see the shell; the presenter narrows and orders the tiles by it
/// (the names starting with the text first), and <see cref="EmptyText"/> says "No gestures match 'xyz'" while nothing is left.
/// </summary>
public sealed class GesturePicker : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<GestureTileItem>> TilesProperty =
        AvaloniaProperty.Register<GesturePicker, IReadOnlyList<GestureTileItem>>(nameof(Tiles), []);

    public static readonly StyledProperty<IReadOnlyList<GestureTile>> TileControlsProperty =
        AvaloniaProperty.Register<GesturePicker, IReadOnlyList<GestureTile>>(nameof(TileControls), []);

    public static readonly StyledProperty<bool> HasSelectionProperty =
        AvaloniaProperty.Register<GesturePicker, bool>(nameof(HasSelection));

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<GesturePicker, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<GesturePicker, string?>(nameof(EmptyText));

    private readonly RowListHost _rows = new(static () => false);
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

    /// <summary>The search box's text (plan 0009), two-way: the presenter narrows <see cref="Tiles"/> by it.</summary>
    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <summary>"No gestures match 'xyz'" while the search leaves no tile; null otherwise (the template hides it).</summary>
    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        private set => SetValue(EmptyTextProperty, value);
    }

    public GestureId? SelectedId => (_rows.SelectedItem as GestureTile)?.Item?.Id ?? _selectedId;

    public void Select(GestureId? id)
    {
        _selectedId = id;
        if (_rows.List is not null)
        {
            _rows.Select(id is { } wanted ? TileControls.FirstOrDefault(tile => tile.Item?.Id == wanted) : null);
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
        // No menu, no keys, no rename: the host gives the picker its selection memory and the double click, which accepts.
        if (_rows.Attach(e.NameScope.Find<ListBox>("PART_Tiles"), OnSelectionChanged, Accept) is not null)
        {
            Select(_selectedId);
        }

        if (e.NameScope.Find<DialogShell>("PART_Shell") is { } shell)
        {
            shell.Confirmed += (_, _) => Accept();
            shell.Cancelled += (_, _) => Cancel();
        }

        e.NameScope.Find<SearchBox.SearchBox>("PART_Search")?.Bind(SearchBox.SearchBox.TextProperty, new Binding(nameof(SearchText)) { Source = this, Mode = BindingMode.TwoWay });
        e.OnClick("PART_NoGesture", ChooseNoGesture);
        e.OnClick("PART_NewGesture", () => NewGestureRequested?.Invoke(this, EventArgs.Empty));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TilesProperty)
        {
            TileControls = [.. Tiles.Select(item => new GestureTile { Item = item })];
            Select(_selectedId);
            ProjectEmptyText();
        }
        else if (change.Property == SearchTextProperty)
        {
            ProjectEmptyText();
        }
    }

    private void ProjectEmptyText()
    {
        var search = SearchQuery.Parse(SearchText);
        EmptyText = Tiles.Count == 0 && !search.IsEmpty ? search.NoneMatch("gestures") : null;
    }

    private void OnSelectionChanged()
    {
        _selectedId = (_rows.SelectedItem as GestureTile)?.Item?.Id ?? _selectedId;
        HasSelection = _rows.SelectedItem is not null;
    }
}
