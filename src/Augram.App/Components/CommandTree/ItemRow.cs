using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// What every row of the Commands tab shares (F5a): an active toggle (the template's <c>PART_Active</c>
/// check box, raising <see cref="ActiveToggled"/> only for a user click) and, when <see cref="CanRename"/>,
/// in-place rename through <c>PART_NameEditor</c>: <see cref="BeginEdit"/> shows it with the current
/// name, Enter or leaving the editor raises <see cref="RenameCommitted"/>, Escape reverts. Rows never touch a
/// store; their list forwards the events to its host. Greyed via <c>:inactive</c>.
/// </summary>
[PseudoClasses(":editing", ":inactive")]
public abstract class ItemRow : TemplatedControl
{
    public static readonly StyledProperty<string> NameTextProperty =
        AvaloniaProperty.Register<ItemRow, string>(nameof(NameText), string.Empty);

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ItemRow, bool>(nameof(IsActive), true);

    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<ItemRow, bool>(nameof(IsEditing));

    public static readonly StyledProperty<bool> CanRenameProperty =
        AvaloniaProperty.Register<ItemRow, bool>(nameof(CanRename), true);

    private TextBox? _editor;
    private CheckBox? _active;

    protected ItemRow()
    {
        AddHandler(PointerPressedEvent, (_, e) => LastPressClickCount = e.ClickCount, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Raised with the trimmed new name when the user presses Enter in the editor.</summary>
    public event EventHandler<string>? RenameCommitted;

    /// <summary>Raised when the user clicks the active check box; the host flips the flag in the store.</summary>
    public event EventHandler? ActiveToggled;

    public string NameText
    {
        get => GetValue(NameTextProperty);
        protected set => SetValue(NameTextProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        protected set => SetValue(IsActiveProperty, value);
    }

    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        private set => SetValue(IsEditingProperty, value);
    }

    /// <summary>The click count of the last press on this row: 2 for the second click of a double click.</summary>
    internal int LastPressClickCount { get; private set; }

    /// <summary>Whether a pointer event landed on one of the row's own controls (a button such as the expander or the active box, or the name editor) rather than on the row itself.</summary>
    internal bool IsFromOwnControl(Visual? source)
    {
        for (var visual = source; visual is not null && visual != this; visual = visual.GetVisualParent())
        {
            if (visual is Button or TextBox)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>False for a section that cannot be renamed (Uncategorized) and for steps.</summary>
    public bool CanRename
    {
        get => GetValue(CanRenameProperty);
        protected set => SetValue(CanRenameProperty, value);
    }

    public void BeginEdit()
    {
        if (!CanRename)
        {
            return;
        }

        IsEditing = true;
        StartEditor();
    }

    public void CancelEdit() => IsEditing = false;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _editor = e.NameScope.Find<TextBox>("PART_NameEditor");
        if (_editor is not null)
        {
            _editor.KeyDown += OnEditorKeyDown;
            _editor.LostFocus += (_, _) => CommitOnLeave();

            // A row created by the store change that made it (a fresh "New command N") is asked to edit
            // before it has a template; the editor starts here instead.
            if (IsEditing)
            {
                StartEditor();
            }
        }

        _active = e.NameScope.Find<CheckBox>("PART_Active");
        if (_active is not null)
        {
            _active.IsChecked = IsActive;
            _active.IsCheckedChanged += OnActiveChanged;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEditingProperty)
        {
            PseudoClasses.Set(":editing", IsEditing);
        }
        else if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(":inactive", !IsActive);
            if (_active is not null && _active.IsChecked != IsActive)
            {
                _active.IsChecked = IsActive;
            }
        }
    }

    private void StartEditor()
    {
        if (_editor is not null)
        {
            _editor.Text = NameText;
            _editor.Focus();
            _editor.SelectAll();
        }
    }

    private void OnActiveChanged(object? sender, EventArgs e)
    {
        if (_active is not null && _active.IsChecked != IsActive)
        {
            ActiveToggled?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Leaving the editor (a click elsewhere, Tab) accepts the name, as Enter does (Joel, 2026-10-07); Escape has already
    /// ended the edit by then. The rename is raised after the input event that moved focus has finished, so a click on
    /// another row selects it first and the store change that follows does not rebuild the rows under that click. An
    /// unchanged name raises nothing.
    /// </summary>
    private void CommitOnLeave()
    {
        if (!IsEditing)
        {
            return;
        }

        var name = _editor?.Text?.Trim() ?? string.Empty;
        IsEditing = false;
        if (!string.Equals(name, NameText, StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(() => RenameCommitted?.Invoke(this, name));
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var name = _editor?.Text?.Trim() ?? string.Empty;
            IsEditing = false;
            RenameCommitted?.Invoke(this, name);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
    }
}
