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
/// <see cref="Forms"/>. A click on a step's header asks to open it, or to close it when it is the open one
/// (Select with no step); the grip only drags (Joel, 0.11.4). <c>PART_New</c> opens the <see cref="StepTypePicker"/>
/// in a flyout; the menu, the keymap and a left-button drag (<see cref="StepDragReorder"/>) end in the same event.
/// Rows are reused when the count is unchanged, so a form survives the store echoing its own edit back.
/// </summary>
public sealed partial class StepList : TemplatedControl
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
    private bool _applying;

    public StepList()
    {
        _drag = new StepDragReorder(() => Rows, () => _list, (from, to) => Raise(StepListAction.Reorder, Rows[from].Item, targetIndex: to), OnDragStarted, OnHeaderClicked);
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list = e.NameScope.Find<ListBox>("PART_Rows");
        if (_list is not null)
        {
            _list.ContextMenu = StepListMenu.Build(Request);
            _list.ContextMenu.Opening += (_, _) => StepListMenu.Refresh(_list.ContextMenu, SelectedStep, CanAddStep);
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
        else if (change.Property == FormContextProperty)
        {
            RebuildForms();
        }
        else
        {
            SyncPicker(change);
        }
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

        // Kept while it shows the step: the one it was built for while it has emitted nothing, or its own last edit echoed
        // back. A step equal to the built one after an emit means the host refused that edit (a rule, plan 0002's Remap
        // output): rebuild, so the form shows what is stored and not what was refused.
        var step = item.Step.Step;
        if (row.Form is not null && (Equals(step, row.LastEmitted) || (row.LastEmitted is null && Equals(step, row.FormBuiltFor))))
        {
            return;
        }

        row.FormBuiltFor = step;
        row.LastEmitted = null;
        row.Form = Forms.Build(
            step,
            next =>
            {
                row.LastEmitted = next;
                Raise(StepListAction.Edit, row.Item, edited: next);
            },
            FormContext);
    }

    /// <summary>
    /// Right-click selects before the context menu opens, so the menu acts on the step under the pointer. A left press on a
    /// step's header or grip goes to the drag tracker and is marked handled, so the list does not select (open) the step on
    /// the press: it opens and closes on the click, and a press that becomes a drag leaves it closed (Joel, 0.11.4). Presses
    /// in the open form and on the active box are theirs. Keyboard focus still moves to the step's container on the press:
    /// Avalonia's focus manager does that on the tunnelling press, before this bubbling handler (StepListFocusTests).
    /// </summary>
    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not StepRow row || _list is null)
        {
            return;
        }

        var properties = e.GetCurrentPoint(row).Properties;
        if (properties.IsRightButtonPressed)
        {
            _list.SelectedItem = row;
        }
        else if (properties.IsLeftButtonPressed && e.Source is Visual source && !row.IsInForm(source) && !row.IsFromOwnControl(source))
        {
            _drag.Press(row, e, clickable: !row.IsOnGrip(e));
            e.Handled = true;
        }
    }

    /// <summary>A click on a closed step's header opens it; on the open one's it closes it, leaving no step selected.</summary>
    private void OnHeaderClicked(StepRow row)
    {
        if (row.Item is { } item)
        {
            Raise(StepListAction.Select, row.IsExpanded ? null : item);
        }
    }

    /// <summary>A drag of the open step closes it first, so its form is not dragged along; the host leaves it closed after the drop.</summary>
    private void OnDragStarted(StepRow row)
    {
        if (row.IsExpanded)
        {
            Raise(StepListAction.Select, null);
        }
    }

    private void Request(StepListAction action)
    {
        var step = SelectedStep;
        switch (action)
        {
            case StepListAction.Add:
                if (CanAddStep)
                {
                    OpenTypePicker();
                }

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
