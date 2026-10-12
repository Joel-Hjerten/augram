using Augram.App.Components.ListEditing;
using Augram.App.Components.Parts;
using Augram.App.Components.SearchBox;
using Augram.App.Declarations;
using Augram.App.Inspector;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;

namespace Augram.App.Components.MasterDetail;

/// <summary>
/// Lookless list-beside-form layout (ADR-0002 §5 <c>MasterDetail</c>; the Exclusions tab, F5): a list of
/// <see cref="MasterItem"/>s as <see cref="MasterRow"/>s (<c>PART_Rows</c>) under a heading and the new button
/// (<c>PART_New</c>, labelled <see cref="NewLabel"/>), the selected item's declared form (<see cref="Detail"/>, rendered by
/// a <c>SectionForm</c> in <c>PART_Detail</c>) beside it, the help line under the list and the message line at the bottom.
/// It looks like a Commands sub-tab and shares its keymap (<see cref="EditKeymap"/>): the rename key or a double click
/// renames in place, Delete deletes, the new and undo keys work, and the right-click menu offers new, rename, delete and, while
/// the host gives it a label (<see cref="MoveLabel"/>), a move to the host's other list.
/// Every intent leaves as one <see cref="ActionRequested"/>; the host owns the selection (<see cref="SelectedId"/>) and the
/// list (<see cref="Items"/>), and the selection survives a rebuild. The toolbar's search box (<c>PART_Search</c>, plan 0009)
/// is two-way bound to <see cref="SearchText"/>, which the host narrows the items by; the find key focuses it, and
/// <see cref="EmptyText"/> says "No apps match 'xyz'" while the search leaves nothing.
/// </summary>
public sealed class MasterDetail : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<MasterItem>> ItemsProperty =
        AvaloniaProperty.Register<MasterDetail, IReadOnlyList<MasterItem>>(nameof(Items), []);

    public static readonly StyledProperty<IReadOnlyList<Control>> RowsProperty =
        AvaloniaProperty.Register<MasterDetail, IReadOnlyList<Control>>(nameof(Rows), []);

    public static readonly StyledProperty<Guid?> SelectedIdProperty =
        AvaloniaProperty.Register<MasterDetail, Guid?>(nameof(SelectedId));

    public static readonly StyledProperty<FormScreen?> DetailProperty =
        AvaloniaProperty.Register<MasterDetail, FormScreen?>(nameof(Detail));

    public static readonly StyledProperty<bool> HasDetailProperty =
        AvaloniaProperty.Register<MasterDetail, bool>(nameof(HasDetail));

    public static readonly StyledProperty<string> HeadingProperty =
        AvaloniaProperty.Register<MasterDetail, string>(nameof(Heading), string.Empty);

    public static readonly StyledProperty<string> NewLabelProperty =
        AvaloniaProperty.Register<MasterDetail, string>(nameof(NewLabel), "New");

    public static readonly StyledProperty<string?> MoveLabelProperty =
        AvaloniaProperty.Register<MasterDetail, string?>(nameof(MoveLabel));

    public static readonly StyledProperty<string> HelpTextProperty =
        AvaloniaProperty.Register<MasterDetail, string>(nameof(HelpText), string.Empty);

    public static readonly StyledProperty<string> EmptyDetailTextProperty =
        AvaloniaProperty.Register<MasterDetail, string>(nameof(EmptyDetailText), string.Empty);

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<MasterDetail, string?>(nameof(Message));

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<MasterDetail, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<MasterDetail, string?>(nameof(EmptyText));

    private readonly RowListHost _rows;
    private SearchBox.SearchBox? _search;

    public MasterDetail()
    {
        _rows = new RowListHost(() => IsEditing, FocusAfterRebuild.SelectedRow);
    }

    public event EventHandler<MasterDetailActionEventArgs>? ActionRequested;

    public IReadOnlyList<MasterItem> Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>The built rows, in order; the template's list shows these.</summary>
    public IReadOnlyList<Control> Rows
    {
        get => GetValue(RowsProperty);
        private set => SetValue(RowsProperty, value);
    }

    public Guid? SelectedId
    {
        get => GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    /// <summary>The selected item's form; null shows <see cref="EmptyDetailText"/> instead.</summary>
    public FormScreen? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public bool HasDetail
    {
        get => GetValue(HasDetailProperty);
        private set => SetValue(HasDetailProperty, value);
    }

    public string Heading
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>The words on the new button and menu entry ("New ignored app").</summary>
    public string NewLabel
    {
        get => GetValue(NewLabelProperty);
        set => SetValue(NewLabelProperty, value);
    }

    /// <summary>The menu's move entry for the selected item ("Move to Per command"); null (the default) shows none.</summary>
    public string? MoveLabel
    {
        get => GetValue(MoveLabelProperty);
        set => SetValue(MoveLabelProperty, value);
    }

    public string HelpText
    {
        get => GetValue(HelpTextProperty);
        set => SetValue(HelpTextProperty, value);
    }

    /// <summary>What the form side says while nothing is selected.</summary>
    public string EmptyDetailText
    {
        get => GetValue(EmptyDetailTextProperty);
        set => SetValue(EmptyDetailTextProperty, value);
    }

    /// <summary>Rule messages and feedback ("Deleted 'X'. Ctrl+Z undoes it.").</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>The search box's text (plan 0009), two-way: the host narrows <see cref="Items"/> by it.</summary>
    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <summary>"No apps match 'xyz'" while the search leaves no row; null otherwise (the template hides it).</summary>
    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        private set => SetValue(EmptyTextProperty, value);
    }

    public SectionForm.SectionForm? DetailPart { get; private set; }

    public MasterItem? SelectedItem => (_rows.SelectedItem as MasterRow)?.Item;

    /// <summary>True while a row's name is being edited in place; the key bindings stand down then.</summary>
    public bool IsEditing => RowListHost.AnyEditing(Rows);

    /// <summary>Selects the item's row and starts renaming it in place.</summary>
    public void BeginRename(Guid id) => _rows.BeginRename(Find(id));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _search = e.NameScope.Find<SearchBox.SearchBox>("PART_Search");
        DetailPart = e.NameScope.Find<SectionForm.SectionForm>("PART_Detail");
        if (DetailPart is not null)
        {
            Region.Mark(DetailPart, "Detail form");
        }

        if (_rows.Attach(e.NameScope.Find<ListBox>("PART_Rows"), () => Raise(MasterDetailAction.Select, SelectedItem)) is { } list)
        {
            Region.Mark(list, Heading.Length > 0 ? Heading : "List");
            list.ContextMenu = MasterDetailMenu.Build(NewLabel, Request);
            list.ContextMenu.Opening += (_, _) => MasterDetailMenu.Refresh(list.ContextMenu, NewLabel, MoveLabel, SelectedItem is not null);
            // On the list, not on this control: the form beside it has text boxes where Delete and the undo key belong to the text.
            list.KeyBindings.Clear();
            MasterDetailMenu.BindKeys(list, EditKeymap.Current, Request, _rows.KeysEnabled);
            ApplySelection();
        }

        e.OnClick("PART_New", () => Request(MasterDetailAction.New));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            Rebuild();
        }
        else if (change.Property == SearchTextProperty)
        {
            ProjectEmptyText();
        }
        else if (change.Property == SelectedIdProperty)
        {
            ApplySelection();
        }
        else if (change.Property == DetailProperty)
        {
            HasDetail = Detail is not null;
        }
    }

    /// <summary>Replaces every row; a list that had the keyboard focus gives it to the selected row's replacement, so the keys keep working.</summary>
    private void Rebuild()
    {
        var rows = new List<Control>(Items.Count);
        foreach (var item in Items)
        {
            var row = new MasterRow { Item = item };
            row.RenameCommitted += (_, name) => Raise(MasterDetailAction.Rename, item, name);
            row.ActiveToggled += (_, _) => Raise(MasterDetailAction.ToggleActive, item);
            row.PointerPressed += OnRowPressed;
            rows.Add(row);
        }

        _rows.Replace(() => Rows = rows, ApplySelection);
        ProjectEmptyText();
    }

    private void ProjectEmptyText()
    {
        var search = SearchQuery.Parse(SearchText);
        EmptyText = Items.Count == 0 && !search.IsEmpty ? search.NoneMatch("apps") : null;
    }

    private MasterRow? Find(Guid id) => Rows.OfType<MasterRow>().FirstOrDefault(row => row.Item?.Id == id);

    private void ApplySelection() => _rows.SelectQuietly(SelectedId is { } id ? Find(id) : null);

    /// <summary>
    /// Right-click selects before the menu opens; a double click on a row's name or summary renames it
    /// (<see cref="ItemRow.IsRenameClick"/>: a row here can always be renamed, anywhere on it).
    /// </summary>
    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not MasterRow row || _rows.List is null)
        {
            return;
        }

        if (!_rows.SelectOnRightPress(row, e))
        {
            _rows.RenameOnDoubleClick(row, e, _rows.BeginRename);
        }
    }

    /// <summary>The button, menu and keys: Rename starts the editor, Find focuses the search box; Delete and Move need a selection; the rest go to the host.</summary>
    private void Request(MasterDetailAction action)
    {
        switch (action)
        {
            case MasterDetailAction.Rename:
                _rows.BeginRename(_rows.SelectedItem as MasterRow);
                break;
            case MasterDetailAction.Find:
                _search?.Focus();
                break;
            case MasterDetailAction.Delete or MasterDetailAction.Move when SelectedItem is { } item:
                Raise(action, item);
                break;
            case MasterDetailAction.Delete or MasterDetailAction.Move:
                break;
            default:
                Raise(action);
                break;
        }
    }

    private void Raise(MasterDetailAction action, MasterItem? item = null, string? name = null)
        => ActionRequested?.Invoke(this, new MasterDetailActionEventArgs(action, item, name));
}
