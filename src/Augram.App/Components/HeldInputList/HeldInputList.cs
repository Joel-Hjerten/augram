using Augram.App.Components.ListEditing;
using Augram.App.Components.Parts;
using Augram.Core.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Augram.App.Components.HeldInputList;

/// <summary>
/// Lookless "Ignore gestures while holding" card of an Exclusions › While holding entry (plan 0012 decisions 2, 5 and 6), laid
/// out like a command's step list: the title line with <c>PART_New</c> ("New…"), then a card whose first row is the "Ignore for:
/// Windows · macOS" switch (<c>PART_Side</c>, a <see cref="PlatformSideBar"/> over <see cref="Side"/>, as "Steps for" is), the
/// recorder line while adding (<c>PART_Picker</c>, a <see cref="HeldInputPicker"/>) and one <see cref="HeldInputRow"/> per
/// input in <c>PART_Rows</c>. Rows reorder by a left drag (<see cref="RowDragReorder{TRow}"/>); the menu and the keys are
/// <see cref="HeldInputListMenu"/>'s. While <see cref="IsReadOnly"/> (the other platform's side, a peek) it is <c>:readonly</c>:
/// no New…, menu or drag, and it raises nothing but Undo, Redo, the switch, Copy from and each row's Copy to
/// (<see cref="CopyToText"/>). Every intent leaves as one <see cref="ActionRequested"/>; the host owns the items and the selection.
/// </summary>
[PseudoClasses(":readonly")]
public sealed class HeldInputList : TemplatedControl
{
    /// <summary>The card's ⓘ.</summary>
    public const string Help = "While you hold any input listed here, a press over this app passes straight through: no gesture, no button trigger, no hold remap starts. "
        + "Any key (S, Space, Alt) or a mouse button; added to the global ignore keys of Options › General. Drag a row to reorder; Delete asks nothing, Undo brings it back.";

    /// <summary>The "Ignore for" row's ⓘ.</summary>
    public const string IgnoreForHelp = "This computer uses its platform's list, and only that one is edited here. The other platform's shows read-only: its own, or converted from this one until it has its own. Copy from replaces the list in view with the other platform's; Copy to on one of its rows adds that input here.";

    public static readonly StyledProperty<IReadOnlyList<HeldInputItem>> ItemsProperty =
        AvaloniaProperty.Register<HeldInputList, IReadOnlyList<HeldInputItem>>(nameof(Items), []);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<HeldInputList, int>(nameof(SelectedIndex), -1);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<HeldInputList, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<PlatformSide?> SideProperty =
        AvaloniaProperty.Register<HeldInputList, PlatformSide?>(nameof(Side));

    public static readonly StyledProperty<string?> CopyToTextProperty =
        AvaloniaProperty.Register<HeldInputList, string?>(nameof(CopyToText));

    public static readonly StyledProperty<bool> IsAddingProperty =
        AvaloniaProperty.Register<HeldInputList, bool>(nameof(IsAdding));

    public static readonly StyledProperty<HostPlatform> PlatformProperty =
        AvaloniaProperty.Register<HeldInputList, HostPlatform>(nameof(Platform), RunningPlatform.Current);

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<HeldInputList, string?>(nameof(EmptyText));

    public static readonly StyledProperty<IReadOnlyList<HeldInputRow>> RowsProperty =
        AvaloniaProperty.Register<HeldInputList, IReadOnlyList<HeldInputRow>>(nameof(Rows), []);

    private readonly RowListHost _rows;
    private readonly RowDragReorder<HeldInputRow> _drag;

    public HeldInputList()
    {
        _rows = new RowListHost(() => IsEditing);
        _drag = new RowDragReorder<HeldInputRow>(() => Rows, () => _rows.List, (from, to) => Raise(HeldInputListAction.Reorder, Rows[from].Item, targetIndex: to), _ => { }, row => Raise(HeldInputListAction.Select, row.Item), () => { });
        ProjectEmptyText();
    }

    public event EventHandler<HeldInputListActionEventArgs>? ActionRequested;

    public IReadOnlyList<HeldInputItem> Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>The selected row, which the keys and the menu act on; -1 for none.</summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The rows shown are the other platform's (plan 0012 decision 5): read, never edited here.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>The "Ignore for" row's side; null hides it.</summary>
    public PlatformSide? Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>"Copy to Windows" on each row of the other platform's side; null shows no such button.</summary>
    public string? CopyToText
    {
        get => GetValue(CopyToTextProperty);
        set => SetValue(CopyToTextProperty, value);
    }

    /// <summary>The recorder line shows (New… was clicked); it closes when an input is chosen or on Cancel.</summary>
    public bool IsAdding
    {
        get => GetValue(IsAddingProperty);
        private set => SetValue(IsAddingProperty, value);
    }

    /// <summary>The side being edited, whose key names the recorder's drop-down reads in.</summary>
    public HostPlatform Platform
    {
        get => GetValue(PlatformProperty);
        set => SetValue(PlatformProperty, value);
    }

    /// <summary>What the card says while it has no row: "None yet …" here, "None on macOS." on the other side; null with rows.</summary>
    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        private set => SetValue(EmptyTextProperty, value);
    }

    public IReadOnlyList<HeldInputRow> Rows
    {
        get => GetValue(RowsProperty);
        private set => SetValue(RowsProperty, value);
    }

    /// <summary>The recorder line (<c>PART_Picker</c>); null before the template is applied.</summary>
    public HeldInputPicker? PickerPart { get; private set; }

    /// <summary>The "Ignore for" row (<c>PART_Side</c>); null before the template is applied.</summary>
    public PlatformSideBar? SidePart { get; private set; }

    public HeldInputItem? SelectedItem => (_rows.SelectedItem as HeldInputRow)?.Item;

    /// <summary>True while the recorder listens or has the keyboard: the list's keys stand down, so a key being recorded never edits the list.</summary>
    public bool IsEditing
    {
        get
        {
            if (PickerPart is not { } picker)
            {
                return false;
            }

            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
            return picker.IsListening || (focused is not null && (ReferenceEquals(picker, focused) || picker.IsVisualAncestorOf(focused)));
        }
    }

    /// <summary>Opens the recorder line (New…); nothing on the other platform's side.</summary>
    public void BeginAdd()
    {
        if (!IsReadOnly)
        {
            PickerPart?.Reset();
            IsAdding = true;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_rows.Attach(e.NameScope.Find<ListBox>("PART_Rows"), OnSelectionChanged) is { } list)
        {
            list.ContextMenu = HeldInputListMenu.Build(Request);
            list.ContextMenu.Opening += (_, args) =>
            {
                args.Cancel = IsReadOnly;
                HeldInputListMenu.Refresh(list.ContextMenu, SelectedItem);
            };
            list.ContextMenu.Closed += (_, _) => ApplySelection();
            KeyBindings.Clear();
            HeldInputListMenu.BindKeys(this, EditKeymap.Current, Request, _rows.KeysEnabled);
            ApplySelection();
        }

        PickerPart = e.NameScope.Find<HeldInputPicker>("PART_Picker");
        if (PickerPart is not null)
        {
            PickerPart.Picked += (_, input) =>
            {
                IsAdding = false;
                Raise(HeldInputListAction.Add, input: input);
            };
            PickerPart.Cancelled += (_, _) => IsAdding = false;
        }

        SidePart = e.NameScope.Find<PlatformSideBar>("PART_Side");
        if (SidePart is not null)
        {
            SidePart.Label = "Ignore for";
            SidePart.Help = IgnoreForHelp;
            SidePart.Side = Side;
            SidePart.SideRequested += (_, platform) => Raise(HeldInputListAction.ShowSide, platform: platform);
            SidePart.CopyRequested += (_, _) => Raise(HeldInputListAction.CopyFromOtherPlatform);
        }

        e.OnClick("PART_New", BeginAdd);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            Rebuild();
        }
        else if (change.Property == SelectedIndexProperty)
        {
            ApplySelection();
        }
        else if (change.Property == SideProperty)
        {
            SidePart?.Side = Side;
            ProjectEmptyText();
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == CopyToTextProperty)
        {
            PseudoClasses.Set(":readonly", IsReadOnly);
            if (IsReadOnly)
            {
                PickerPart?.Reset();
                IsAdding = false;
            }

            foreach (var row in Rows)
            {
                Style(row);
            }

            ProjectEmptyText();
        }
    }

    private void Rebuild()
    {
        var items = Items;
        if (Rows.Count == items.Count)
        {
            for (var i = 0; i < items.Count; i++)
            {
                Rows[i].Item = items[i];
            }

            ApplySelection();
        }
        else
        {
            var rows = new List<HeldInputRow>(items.Count);
            foreach (var item in items)
            {
                var row = new HeldInputRow { Item = item };
                Style(row);
                row.PointerPressed += OnRowPressed;
                row.CopyToRequested += (_, _) => Raise(HeldInputListAction.CopyTo, row.Item);
                _drag.Attach(row);
                rows.Add(row);
            }

            _rows.Replace(() => Rows = rows, ApplySelection);
        }

        ProjectEmptyText();
    }

    private void Style(HeldInputRow row)
    {
        row.IsReadOnly = IsReadOnly;
        row.CopyToText = IsReadOnly ? CopyToText : null;
    }

    private void ProjectEmptyText() => EmptyText = Items.Count > 0 ? null
        : IsReadOnly ? $"None on {Side?.Shown.DisplayName() ?? "that platform"}."
        : "None yet: New… records a key or a mouse button, or picks one.";

    private void OnSelectionChanged()
    {
        if (SelectedItem is { } item && item.Index != SelectedIndex)
        {
            Raise(HeldInputListAction.Select, item);
        }
    }

    private void ApplySelection() => _rows.SelectQuietly(Rows.FirstOrDefault(row => row.Item?.Index == SelectedIndex));

    /// <summary>
    /// Right-click selects the row under the pointer before the menu opens; a left press anywhere on the row goes to the drag
    /// tracker and is marked handled, so a click selects (on release) and a press that becomes a drag only moves the row, as on
    /// the step list. Presses on the row's own buttons are theirs; the other platform's rows take none.
    /// </summary>
    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not HeldInputRow row || _rows.List is null || IsReadOnly)
        {
            return;
        }

        if (!_rows.SelectOnRightPress(row, e, quietly: true) && e.GetCurrentPoint(row).Properties.IsLeftButtonPressed && e.Source is Visual source && !row.IsFromOwnControl(source))
        {
            _drag.Press(row, e, clickable: !row.IsOnGrip(e));
            e.Handled = true;
        }
    }

    private void Request(HeldInputListAction action)
    {
        var item = SelectedItem;
        switch (action)
        {
            case HeldInputListAction.Add:
                BeginAdd();
                return;
            case HeldInputListAction.Copy or HeldInputListAction.Delete when item is null:
                return;
            case HeldInputListAction.Paste or HeldInputListAction.Undo or HeldInputListAction.Redo:
                Raise(action);
                return;
            default:
                Raise(action, item);
                return;
        }
    }

    private void Raise(HeldInputListAction action, HeldInputItem? item = null, Augram.Core.Mapping.KeyOrButton? input = null, int targetIndex = -1, HostPlatform? platform = null)
    {
        var allowed = !IsReadOnly || action is HeldInputListAction.Undo or HeldInputListAction.Redo or HeldInputListAction.CopyTo or HeldInputListAction.ShowSide or HeldInputListAction.CopyFromOtherPlatform;
        if (allowed)
        {
            ActionRequested?.Invoke(this, new HeldInputListActionEventArgs(action, item, input, targetIndex, platform));
        }
    }
}
