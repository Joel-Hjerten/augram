using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>
/// One modifier in the Hotkey form's hand editor (F5): a check box for the modifier and, beside it, a
/// small "R" toggle for its right-hand key ("RAlt"), enabled only while the modifier is on. Parts:
/// <c>PART_On</c> and <c>PART_Right</c> (toggle buttons; label and tooltip come from the theme).
/// <see cref="Toggled"/> is raised when the user changes either part, never when <see cref="IsOn"/> or
/// <see cref="IsRightHand"/> are set from code. With the modifier off the "R" part shows unchecked, and a
/// user change leaves <see cref="IsRightHand"/> false.
/// </summary>
public sealed class ModifierToggle : TemplatedControl
{
    public static readonly StyledProperty<bool> IsOnProperty = AvaloniaProperty.Register<ModifierToggle, bool>(nameof(IsOn));

    public static readonly StyledProperty<bool> IsRightHandProperty = AvaloniaProperty.Register<ModifierToggle, bool>(nameof(IsRightHand));

    private ToggleButton? _on;
    private ToggleButton? _right;
    private bool _syncing;

    public event EventHandler? Toggled;

    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    public bool IsRightHand { get => GetValue(IsRightHandProperty); set => SetValue(IsRightHandProperty, value); }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Watch(_on, false);
        Watch(_right, false);
        _on = e.NameScope.Find<ToggleButton>("PART_On");
        _right = e.NameScope.Find<ToggleButton>("PART_Right");
        Sync();
        Watch(_on, true);
        Watch(_right, true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOnProperty || change.Property == IsRightHandProperty)
        {
            Sync();
        }
    }

    private void Watch(ToggleButton? part, bool on)
    {
        if (part is null)
        {
            return;
        }

        if (on)
        {
            part.IsCheckedChanged += OnPartChanged;
        }
        else
        {
            part.IsCheckedChanged -= OnPartChanged;
        }
    }

    private void OnPartChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        IsOn = _on?.IsChecked == true;
        IsRightHand = IsOn && _right?.IsChecked == true;
        Sync();
        Toggled?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Parts follow the properties; their change events are ignored meanwhile.</summary>
    private void Sync()
    {
        _syncing = true;
        try
        {
            if (_on is not null)
            {
                _on.IsChecked = IsOn;
            }

            if (_right is not null)
            {
                _right.IsChecked = IsOn && IsRightHand;
                _right.IsEnabled = IsOn;
            }
        }
        finally
        {
            _syncing = false;
        }
    }
}
