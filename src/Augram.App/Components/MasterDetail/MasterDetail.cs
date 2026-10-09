using Augram.App.Components.CommandTree;
using Augram.App.Declarations;
using Augram.App.Inspector;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace Augram.App.Components.MasterDetail;

/// <summary>
/// Lookless list-beside-form layout (ADR-0002 §5 <c>MasterDetail</c>; the Ignored tab, F5): a list of
/// <see cref="MasterItem"/>s as <see cref="MasterRow"/>s (<c>PART_Rows</c>) under a heading and the new button
/// (<c>PART_New</c>, labelled <see cref="NewLabel"/>), the selected item's declared form (<see cref="Detail"/>, rendered by
/// a <c>SectionForm</c> in <c>PART_Detail</c>) beside it, the help line under the list and the message line at the bottom.
/// It looks like a Commands sub-tab and shares its keymap (<see cref="CommandsKeymap"/>): the rename key or a double click
/// renames in place, Delete deletes, the new and undo keys work, and the right-click menu offers new, rename and delete.
/// Every intent leaves as one <see cref="ActionRequested"/>; the host owns the selection (<see cref="SelectedId"/>) and the
/// list (<see cref="Items"/>), and the selection survives a rebuild.
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

    public static readonly StyledProperty<string> HelpTextProperty =
        AvaloniaProperty.Register<MasterDetail, string>(nameof(HelpText), string.Empty);

    public static readonly StyledProperty<string> EmptyDetailTextProperty =
        AvaloniaProperty.Register<MasterDetail, string>(nameof(EmptyDetailText), string.Empty);

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<MasterDetail, string?>(nameof(Message));

    private ListBox? _list;
    private bool _applying;

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

    public SectionForm.SectionForm? DetailPart { get; private set; }

    public MasterItem? SelectedItem => (_list?.SelectedItem as MasterRow)?.Item;

    /// <summary>True while a row's name is being edited in place; the key bindings stand down then.</summary>
    public bool IsEditing => Rows.Any(row => row is ItemRow { IsEditing: true });

    /// <summary>Selects the item's row and starts renaming it in place.</summary>
    public void BeginRename(Guid id) => BeginRename(Find(id));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetailPart = e.NameScope.Find<SectionForm.SectionForm>("PART_Detail");
        if (DetailPart is not null)
        {
            Region.Mark(DetailPart, "Detail form");
        }

        _list = e.NameScope.Find<ListBox>("PART_Rows");
        if (_list is not null)
        {
            Region.Mark(_list, Heading.Length > 0 ? Heading : "List");
            _list.ContextMenu = MasterDetailMenu.Build(NewLabel, Request);
            _list.ContextMenu.Opening += (_, _) => MasterDetailMenu.Refresh(_list.ContextMenu, NewLabel, SelectedItem is not null);
            // On the list, not on this control: the form beside it has text boxes where Delete and the undo key belong to the text.
            _list.KeyBindings.Clear();
            MasterDetailMenu.BindKeys(_list, CommandsKeymap.Current, Request, () => !IsEditing);
            _list.SelectionChanged += (_, _) =>
            {
                if (!_applying)
                {
                    Raise(MasterDetailAction.Select, SelectedItem);
                }
            };
            ApplySelection();
        }

        if (e.NameScope.Find<Button>("PART_New") is { } newButton)
        {
            newButton.Click += (_, _) => Request(MasterDetailAction.New);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            Rebuild();
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
        var hadFocus = _list?.IsKeyboardFocusWithin == true;
        var rows = new List<Control>(Items.Count);
        foreach (var item in Items)
        {
            var row = new MasterRow { Item = item };
            row.RenameCommitted += (_, name) => Raise(MasterDetailAction.Rename, item, name);
            row.ActiveToggled += (_, _) => Raise(MasterDetailAction.ToggleActive, item);
            row.PointerPressed += OnRowPressed;
            rows.Add(row);
        }

        // Replacing the rows clears the list's selection at once; that is not the user selecting nothing.
        Quietly(() => Rows = rows);
        ApplySelection();
        if (hadFocus)
        {
            Dispatcher.UIThread.Post(() => (_list?.SelectedItem is { } selected ? _list.ContainerFromItem(selected) : null)?.Focus(), DispatcherPriority.Loaded);
        }
    }

    private MasterRow? Find(Guid id) => Rows.OfType<MasterRow>().FirstOrDefault(row => row.Item?.Id == id);

    private void ApplySelection()
    {
        if (_list is not null)
        {
            Quietly(() => _list.SelectedItem = SelectedId is { } id ? Find(id) : null);
        }
    }

    private void Quietly(Action change)
    {
        _applying = true;
        try
        {
            change();
        }
        finally
        {
            _applying = false;
        }
    }

    private void BeginRename(MasterRow? row)
    {
        if (_list is not null && row is not null)
        {
            _list.SelectedItem = row;
            row.BeginEdit();
        }
    }

    /// <summary>Right-click selects before the menu opens; a double click on a row's name or summary renames it.</summary>
    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not MasterRow row || _list is null)
        {
            return;
        }

        var properties = e.GetCurrentPoint(row).Properties;
        if (properties.IsRightButtonPressed)
        {
            _list.SelectedItem = row;
        }
        else if (properties.IsLeftButtonPressed && e.ClickCount == 2 && !row.IsEditing && !row.IsFromOwnControl(e.Source as Visual))
        {
            Dispatcher.UIThread.Post(() => BeginRename(row));
        }
    }

    /// <summary>The button, menu and keys: Rename starts the editor; Delete needs a selection; the rest go to the host.</summary>
    private void Request(MasterDetailAction action)
    {
        switch (action)
        {
            case MasterDetailAction.Rename:
                BeginRename(_list?.SelectedItem as MasterRow);
                break;
            case MasterDetailAction.Delete when SelectedItem is { } item:
                Raise(action, item);
                break;
            case MasterDetailAction.Delete:
                break;
            default:
                Raise(action);
                break;
        }
    }

    private void Raise(MasterDetailAction action, MasterItem? item = null, string? name = null)
        => ActionRequested?.Invoke(this, new MasterDetailActionEventArgs(action, item, name));
}
