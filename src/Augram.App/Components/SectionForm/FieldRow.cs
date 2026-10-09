using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.SectionForm;

/// <summary>
/// Lookless row of a <see cref="SectionForm"/>: label and help on one side, the field's editor on the
/// other, with the field's optional <see cref="Accessory"/> just before the editor. The editor comes from
/// the field kind's renderer; this row owns none of its behaviour.
/// </summary>
public sealed class FieldRow : TemplatedControl
{
    public static readonly StyledProperty<Control?> AccessoryProperty =
        AvaloniaProperty.Register<FieldRow, Control?>(nameof(Accessory));

    public static readonly DirectProperty<FieldRow, bool> HasAccessoryProperty =
        AvaloniaProperty.RegisterDirect<FieldRow, bool>(nameof(HasAccessory), row => row.HasAccessory);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<FieldRow, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string?> HelpProperty =
        AvaloniaProperty.Register<FieldRow, string?>(nameof(Help));

    public static readonly StyledProperty<Control?> EditorProperty =
        AvaloniaProperty.Register<FieldRow, Control?>(nameof(Editor));

    public static readonly DirectProperty<FieldRow, bool> HasHelpProperty =
        AvaloniaProperty.RegisterDirect<FieldRow, bool>(nameof(HasHelp), row => row.HasHelp);

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Help
    {
        get => GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
    }

    public Control? Editor
    {
        get => GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public bool HasHelp => !string.IsNullOrEmpty(Help);

    /// <summary>The field's small control before the editor (<c>Field.Accessory</c>), or null.</summary>
    public Control? Accessory
    {
        get => GetValue(AccessoryProperty);
        set => SetValue(AccessoryProperty, value);
    }

    /// <summary>The template takes no room for an accessory the field does not have.</summary>
    public bool HasAccessory => Accessory is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HelpProperty)
        {
            RaisePropertyChanged(HasHelpProperty, !HasHelp, HasHelp);
        }
        else if (change.Property == AccessoryProperty)
        {
            RaisePropertyChanged(HasAccessoryProperty, change.OldValue is not null, HasAccessory);
        }
    }
}
