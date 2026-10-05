using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.SectionForm;

/// <summary>Lookless titled group of <see cref="FieldRow"/>s inside a <see cref="SectionForm"/>.</summary>
public sealed class SectionView : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SectionView, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> HelpProperty =
        AvaloniaProperty.Register<SectionView, string?>(nameof(Help));

    public static readonly StyledProperty<IReadOnlyList<Control>> RowsProperty =
        AvaloniaProperty.Register<SectionView, IReadOnlyList<Control>>(nameof(Rows), []);

    public static readonly DirectProperty<SectionView, bool> HasHelpProperty =
        AvaloniaProperty.RegisterDirect<SectionView, bool>(nameof(HasHelp), view => view.HasHelp);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Help
    {
        get => GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
    }

    public IReadOnlyList<Control> Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public bool HasHelp => !string.IsNullOrEmpty(Help);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HelpProperty)
        {
            RaisePropertyChanged(HasHelpProperty, !HasHelp, HasHelp);
        }
    }
}
