using Augram.App.Components.ListEditing;
using Augram.App.Components.Parts;
using Augram.App.Components.Steps;
using Augram.Core.Steps;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Augram.App.Components.StepList;

/// <summary>
/// Lookless step list of the selected command (F5a): <see cref="Steps"/> in, one
/// <see cref="StepListActionEventArgs"/> out per intent. One <see cref="StepRow"/> per step in
/// <c>PART_Rows</c>; the row at <see cref="SelectedIndex"/> is expanded with its type's form from
/// <see cref="Forms"/>. A click on a step's header asks to open it, or to close it when it is the open one
/// (Select with no step); the grip only drags (Joel, 0.11.4). <c>PART_New</c> opens the <see cref="StepTypePicker"/>
/// in a flyout, and the menu's "Change to…" another at the selected step's header (<c>StepList.Picker.cs</c>); the menu, the
/// keymap and a left-button drag (<see cref="StepDragReorder"/>) end in the same event.
/// Rows are reused when the count is unchanged, so a form survives the store echoing its own edit back. While
/// <see cref="IsReadOnly"/> (plan 0010: the other platform's steps, a peek) it is <c>:readonly</c>: no New step…, no menu,
/// no drag, no step opens, and it raises nothing but Undo and Redo. <see cref="CardHeader"/>, when set, is drawn as the card's
/// first row above the steps, split from them as a form card's rows are (the workbench puts the "Steps for" row there, 0.14.3).
/// </summary>
[PseudoClasses(":readonly")]
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

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<StepList, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<object?> CardHeaderProperty =
        AvaloniaProperty.Register<StepList, object?>(nameof(CardHeader));

    public static readonly StyledProperty<IReadOnlyList<StepRow>> RowsProperty =
        AvaloniaProperty.Register<StepList, IReadOnlyList<StepRow>>(nameof(Rows), []);

    private readonly RowListHost _rows;
    private readonly StepDragReorder _drag;
    private Button? _new;

    public StepList()
    {
        _rows = new RowListHost(() => IsEditing);
        _drag = new StepDragReorder(() => Rows, () => _rows.List, (from, to) => Raise(StepListAction.Reorder, Rows[from].Item, targetIndex: to), OnDragStarted, OnHeaderClicked);
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

    /// <summary>The steps shown are the other platform's (plan 0010 decision 6): read, never edited here.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>The card's first row, above the steps (a control); null draws none. The list never looks inside it.</summary>
    public object? CardHeader
    {
        get => GetValue(CardHeaderProperty);
        set => SetValue(CardHeaderProperty, value);
    }

    public IReadOnlyList<StepRow> Rows
    {
        get => GetValue(RowsProperty);
        private set => SetValue(RowsProperty, value);
    }

    /// <summary>Swappable for tests and the gallery; defaults to the assembly-scanned registry.</summary>
    public StepFormRegistry Forms { get; init; } = StepFormRegistry.Default;

    public StepItem? SelectedStep => (_rows.SelectedItem as StepRow)?.Item;

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
        if (_rows.Attach(e.NameScope.Find<ListBox>("PART_Rows"), OnSelectionChanged) is { } list)
        {
            list.ContextMenu = StepListMenu.Build(Request);
            list.ContextMenu.Opening += (_, args) =>
            {
                args.Cancel = IsReadOnly;
                StepListMenu.Refresh(list.ContextMenu, SelectedStep, CanAddStep);
            };
            KeyBindings.Clear();
            StepListMenu.BindKeys(this, EditKeymap.Current, Request, _rows.KeysEnabled);
            ApplySelection();
        }

        _new = e.OnClick("PART_New", OpenTypePicker);
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
        else if (change.Property == IsReadOnlyProperty)
        {
            PseudoClasses.Set(":readonly", IsReadOnly);
            foreach (var row in Rows)
            {
                row.IsReadOnly = IsReadOnly;
            }
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
                var row = new StepRow { Item = step, IsReadOnly = IsReadOnly };
                row.ActiveToggled += (_, _) => Raise(StepListAction.ToggleActive, row.Item);
                row.PointerPressed += OnRowPressed;
                _drag.Attach(row);
                rows.Add(row);
            }

            // Rows are reused while the count is unchanged (above), so only a new set goes through the host; the focus stays
            // where Avalonia put it on the press (FocusAfterRebuild.None).
            _rows.Replace(() => Rows = rows, ApplySelection);
            return;
        }

        ApplySelection();
    }

    /// <summary>A user selection of another step asks to open it; reselecting the open one raises nothing.</summary>
    private void OnSelectionChanged()
    {
        if (SelectedStep is { } step && step.Index != SelectedIndex)
        {
            Raise(StepListAction.Select, step);
        }
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

        _rows.SelectQuietly(selected);
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
        if (sender is not StepRow row || _rows.List is null || IsReadOnly)
        {
            return;
        }

        if (!_rows.SelectOnRightPress(row, e) && e.GetCurrentPoint(row).Properties.IsLeftButtonPressed && e.Source is Visual source && !row.IsInForm(source) && !row.IsFromOwnControl(source))
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

    /// <summary>
    /// Opens a picker once the menu that asked for it has closed: a flyout shown while the context menu is still open never
    /// closed on a click elsewhere (Joel, 0.11.6), since the menu's closing took the light dismiss with it. From a key it only
    /// waits a dispatcher turn.
    /// </summary>
    private static void AfterMenu(Action open) => Dispatcher.UIThread.Post(open, DispatcherPriority.Background);

    private void Request(StepListAction action)
    {
        var step = SelectedStep;
        switch (action)
        {
            case StepListAction.Add:
                if (CanAddStep)
                {
                    AfterMenu(OpenTypePicker);
                }

                return;
            case StepListAction.ChangeType:
                if (step is not null)
                {
                    AfterMenu(() => OpenChangePicker(step));
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
    {
        if (!IsReadOnly || action is StepListAction.Undo or StepListAction.Redo)
        {
            ActionRequested?.Invoke(this, new StepListActionEventArgs(action, step, type, edited, targetIndex));
        }
    }
}
