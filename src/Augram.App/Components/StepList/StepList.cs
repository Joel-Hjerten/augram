using Augram.App.Components.CommandTree;
using Augram.App.Components.Steps;
using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Augram.App.Components.StepList;

/// <summary>
/// Lookless step list of the selected command (F5a): <see cref="Steps"/> in, one
/// <see cref="StepListActionEventArgs"/> out per intent. One <see cref="StepRow"/> per step in
/// <c>PART_Rows</c>; the row at <see cref="SelectedIndex"/> is expanded with its type's form from
/// <see cref="Forms"/>. <c>PART_New</c> opens the <see cref="StepTypePicker"/> in a flyout; the menu,
/// the keymap and a left-button drag (<see cref="StepDragReorder"/>) end in the same event. Rows are
/// reused when the count is unchanged, so a form survives the store echoing its own edit back.
/// </summary>
public sealed class StepList : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<StepItem>> StepsProperty =
        AvaloniaProperty.Register<StepList, IReadOnlyList<StepItem>>(nameof(Steps), []);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<StepList, int>(nameof(SelectedIndex), -1);

    public static readonly StyledProperty<IReadOnlyList<IStepType>> StepTypesProperty =
        AvaloniaProperty.Register<StepList, IReadOnlyList<IStepType>>(nameof(StepTypes), []);

    public static readonly StyledProperty<bool> HasCommandProperty =
        AvaloniaProperty.Register<StepList, bool>(nameof(HasCommand));

    public static readonly StyledProperty<IReadOnlyList<StepRow>> RowsProperty =
        AvaloniaProperty.Register<StepList, IReadOnlyList<StepRow>>(nameof(Rows), []);

    private readonly StepDragReorder _drag;
    private ListBox? _list;
    private Button? _new;
    private StepTypePicker.StepTypePicker? _picker;
    private Flyout? _flyout;
    private bool _applying;

    public StepList()
    {
        _drag = new StepDragReorder(() => Rows, () => _list, (from, to) => Raise(StepListAction.Reorder, Rows[from].Item, targetIndex: to));
    }

    public event EventHandler<StepListActionEventArgs>? ActionRequested;

    public IReadOnlyList<StepItem> Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    /// <summary>The expanded step; -1 for none.</summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>What "New step…" offers; the picker drops the <c>Other</c> category itself.</summary>
    public IReadOnlyList<IStepType> StepTypes
    {
        get => GetValue(StepTypesProperty);
        set => SetValue(StepTypesProperty, value);
    }

    /// <summary>False while no command is selected: nothing to add a step to.</summary>
    public bool HasCommand
    {
        get => GetValue(HasCommandProperty);
        set => SetValue(HasCommandProperty, value);
    }

    public IReadOnlyList<StepRow> Rows
    {
        get => GetValue(RowsProperty);
        private set => SetValue(RowsProperty, value);
    }

    /// <summary>Swappable for tests and the gallery; defaults to the assembly-scanned registry.</summary>
    public StepFormRegistry Forms { get; init; } = StepFormRegistry.Default;

    /// <summary>The picker "New step…" shows; built on first use so tests can choose a type without a flyout.</summary>
    public StepTypePicker.StepTypePicker TypePicker => _picker ??= BuildPicker();

    public StepItem? SelectedStep => (_list?.SelectedItem as StepRow)?.Item;

    /// <summary>True while keyboard focus is inside the expanded step's form; the list's key bindings stand down then.</summary>
    public bool IsEditing
    {
        get
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
            return focused is not null && Rows.Any(row => row.Form is { } form && (ReferenceEquals(form, focused) || form.IsVisualAncestorOf(focused)));
        }
    }

    public void OpenTypePicker()
    {
        if (_new is null)
        {
            return;
        }

        _flyout ??= new Flyout { Content = TypePicker, Placement = PlacementMode.BottomEdgeAlignedLeft };
        _flyout.ShowAt(_new);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list = e.NameScope.Find<ListBox>("PART_Rows");
        if (_list is not null)
        {
            _list.ContextMenu = StepListMenu.Build(Request);
            _list.ContextMenu.Opening += (_, _) => StepListMenu.Refresh(_list.ContextMenu, SelectedStep);
            KeyBindings.Clear();
            StepListMenu.BindKeys(this, CommandsKeymap.Current, Request, () => !IsEditing);
            _list.SelectionChanged += (_, _) =>
            {
                if (!_applying && SelectedStep is { } step && step.Index != SelectedIndex)
                {
                    Raise(StepListAction.Select, step);
                }
            };
            ApplySelection();
        }

        _new = e.NameScope.Find<Button>("PART_New");
        if (_new is not null)
        {
            _new.Click += (_, _) => OpenTypePicker();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StepsProperty)
        {
            Rebuild();
        }
        else if (change.Property == SelectedIndexProperty)
        {
            ApplySelection();
        }
        else if (change.Property == StepTypesProperty && _picker is not null)
        {
            _picker.Types = StepTypes;
        }
    }

    private StepTypePicker.StepTypePicker BuildPicker()
    {
        var picker = new StepTypePicker.StepTypePicker { Types = StepTypes };
        picker.TypeChosen += (_, type) =>
        {
            _flyout?.Hide();
            Raise(StepListAction.Add, null, type: type);
        };
        return picker;
    }

    private void Rebuild()
    {
        var steps = Steps;
        if (Rows.Count == steps.Count)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                Rows[i].Item = steps[i];
            }
        }
        else
        {
            var rows = new List<StepRow>(steps.Count);
            foreach (var step in steps)
            {
                var row = new StepRow { Item = step };
                row.ActiveToggled += (_, _) => Raise(StepListAction.ToggleActive, row.Item);
                row.PointerPressed += OnRowPressed;
                _drag.Attach(row);
                rows.Add(row);
            }

            // Replacing the rows clears the list's selection at once; that is not the user selecting nothing.
            _applying = true;
            try
            {
                Rows = rows;
            }
            finally
            {
                _applying = false;
            }
        }

        ApplySelection();
    }

    /// <summary>Expands the row at <see cref="SelectedIndex"/> (building or keeping its form) and selects it without raising Select.</summary>
    private void ApplySelection()
    {
        StepRow? selected = null;
        foreach (var row in Rows)
        {
            var expanded = row.Item?.Index == SelectedIndex;
            row.IsExpanded = expanded;
            if (expanded)
            {
                selected = row;
                EnsureForm(row);
            }
            else
            {
                row.Form = null;
                row.FormBuiltFor = null;
                row.LastEmitted = null;
            }
        }

        if (_list is null)
        {
            return;
        }

        _applying = true;
        try
        {
            _list.SelectedItem = selected;
        }
        finally
        {
            _applying = false;
        }
    }

    private void EnsureForm(StepRow row)
    {
        if (row.Item is not { } item)
        {
            return;
        }

        var step = item.Step.Step;
        if (row.Form is not null && (Equals(step, row.FormBuiltFor) || Equals(step, row.LastEmitted)))
        {
            return;
        }

        row.FormBuiltFor = step;
        row.LastEmitted = null;
        row.Form = Forms.Build(step, next =>
        {
            row.LastEmitted = next;
            Raise(StepListAction.Edit, row.Item, edited: next);
        });
    }

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is StepRow row && _list is not null && e.GetCurrentPoint(row).Properties.IsRightButtonPressed)
        {
            _list.SelectedItem = row;
        }
    }

    private void Request(StepListAction action)
    {
        var step = SelectedStep;
        switch (action)
        {
            case StepListAction.Add:
                OpenTypePicker();
                return;
            case StepListAction.Duplicate or StepListAction.Copy or StepListAction.Delete or StepListAction.ToggleActive when step is null:
                return;
            case StepListAction.Paste or StepListAction.Undo or StepListAction.Redo:
                Raise(action, null);
                return;
            default:
                Raise(action, step);
                return;
        }
    }

    private void Raise(StepListAction action, StepItem? step, IStepType? type = null, IStep? edited = null, int targetIndex = -1)
        => ActionRequested?.Invoke(this, new StepListActionEventArgs(action, step, type, edited, targetIndex));
}
