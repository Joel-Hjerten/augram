using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// What every row of the Commands tab shares (F5a): an active toggle (the template's <c>PART_Active</c>
/// check box, raising <see cref="ActiveToggled"/> only for a user click) and, when <see cref="CanRename"/>,
/// in-place rename through <c>PART_NameEditor</c>: <see cref="BeginEdit"/> shows it with the current
/// name, Enter raises <see cref="RenameCommitted"/>, Escape and losing focus revert. Rows never touch a
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

    /// <summary>False for the Global group (F5a: never renamed) and for steps.</summary>
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
        if (_editor is not null)
        {
            _editor.Text = NameText;
            _editor.Focus();
            _editor.SelectAll();
        }
    }

    public void CancelEdit() => IsEditing = false;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _editor = e.NameScope.Find<TextBox>("PART_NameEditor");
        if (_editor is not null)
        {
            _editor.KeyDown += OnEditorKeyDown;
            _editor.LostFocus += (_, _) => CancelEdit();
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

    private void OnActiveChanged(object? sender, EventArgs e)
    {
        if (_active is not null && _active.IsChecked != IsActive)
        {
            ActiveToggled?.Invoke(this, EventArgs.Empty);
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
