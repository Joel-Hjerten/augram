using Augram.App.Components.Fields;
using Augram.App.Declarations;
using Augram.App.Inspector;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.SectionForm;

/// <summary>
/// Renders a <see cref="FormScreen"/> (ADR-0002 §5c): one <see cref="SectionView"/> per section, one
/// <see cref="FieldRow"/> per field, the editor from <see cref="FieldRendererRegistry"/>. Sets
/// <see cref="Region"/> marks on every node so the F1 inspector can name them. Knows no field kind.
/// </summary>
public sealed class SectionForm : TemplatedControl
{
    public static readonly StyledProperty<FormScreen?> ScreenProperty =
        AvaloniaProperty.Register<SectionForm, FormScreen?>(nameof(Screen));

    public static readonly StyledProperty<IReadOnlyList<Control>> SectionsProperty =
        AvaloniaProperty.Register<SectionForm, IReadOnlyList<Control>>(nameof(Sections), []);

    public FormScreen? Screen
    {
        get => GetValue(ScreenProperty);
        set => SetValue(ScreenProperty, value);
    }

    /// <summary>The built section controls; the template lists them.</summary>
    public IReadOnlyList<Control> Sections
    {
        get => GetValue(SectionsProperty);
        private set => SetValue(SectionsProperty, value);
    }

    /// <summary>Swappable for tests and the gallery; defaults to the assembly-scanned registry.</summary>
    public FieldRendererRegistry Renderers { get; init; } = FieldRendererRegistry.Default;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScreenProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var screen = Screen;
        if (screen is null)
        {
            Sections = [];
            return;
        }

        Region.Mark(this, screen.Title, new RegionInfo(screen.Title, "Form", screen.Source));
        Sections = [.. screen.Sections.Select(section => BuildSection(screen, section))];
    }

    private SectionView BuildSection(FormScreen screen, Section section)
    {
        var path = $"{screen.Title} › {section.Title}";
        var view = new SectionView
        {
            Title = section.Title,
            Help = section.Help,
            Rows = [.. section.Fields.Select(field => BuildRow(path, field))],
        };
        Region.Mark(view, section.Title, new RegionInfo(path, "Section", section.Source));
        return view;
    }

    private FieldRow BuildRow(string sectionPath, Field field)
    {
        var row = new FieldRow { Label = field.Label, Help = field.Help, Editor = Renderers.Build(field) };
        Region.Mark(row, field.Label, new RegionInfo($"{sectionPath} › {field.Label}", field.Kind, field.Source, field.Binding?.PropertyName));
        if (field.Visible is { } visible)
        {
            BindingObserver.Attach(row, visible, value => row.IsVisible = value);
        }

        return row;
    }
}
