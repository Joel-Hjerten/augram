using Augram.Core.Gestures;
using Avalonia;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless command row of the <see cref="CommandTree"/> (F5a): the gesture's glyph, or a trigger
/// badge ("Wheel up", "No trigger") when there is no glyph to draw, the name (editable in place), the
/// step summary, the F8 platform marker when the command has one, and the active toggle.
/// </summary>
public sealed class CommandRow : ItemRow
{
    public static readonly StyledProperty<CommandItem?> ItemProperty =
        AvaloniaProperty.Register<CommandRow, CommandItem?>(nameof(Item));

    public static readonly StyledProperty<string> TriggerTextProperty =
        AvaloniaProperty.Register<CommandRow, string>(nameof(TriggerText), string.Empty);

    public static readonly StyledProperty<string> SummaryTextProperty =
        AvaloniaProperty.Register<CommandRow, string>(nameof(SummaryText), string.Empty);

    public static readonly StyledProperty<string?> MarkerTextProperty =
        AvaloniaProperty.Register<CommandRow, string?>(nameof(MarkerText));

    public static readonly StyledProperty<IReadOnlyList<GesturePoint>?> PointsProperty =
        AvaloniaProperty.Register<CommandRow, IReadOnlyList<GesturePoint>?>(nameof(Points));

    public static readonly StyledProperty<bool> HasGlyphProperty =
        AvaloniaProperty.Register<CommandRow, bool>(nameof(HasGlyph));

    public static readonly StyledProperty<bool> HasMarkerProperty =
        AvaloniaProperty.Register<CommandRow, bool>(nameof(HasMarker));

    public CommandItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public string TriggerText
    {
        get => GetValue(TriggerTextProperty);
        private set => SetValue(TriggerTextProperty, value);
    }

    public string SummaryText
    {
        get => GetValue(SummaryTextProperty);
        private set => SetValue(SummaryTextProperty, value);
    }

    public string? MarkerText
    {
        get => GetValue(MarkerTextProperty);
        private set => SetValue(MarkerTextProperty, value);
    }

    public IReadOnlyList<GesturePoint>? Points
    {
        get => GetValue(PointsProperty);
        private set => SetValue(PointsProperty, value);
    }

    public bool HasGlyph
    {
        get => GetValue(HasGlyphProperty);
        private set => SetValue(HasGlyphProperty, value);
    }

    public bool HasMarker
    {
        get => GetValue(HasMarkerProperty);
        private set => SetValue(HasMarkerProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            var item = Item;
            NameText = item?.Name ?? string.Empty;
            IsActive = item?.IsActive ?? true;
            TriggerText = item?.TriggerText ?? string.Empty;
            SummaryText = item?.StepSummary ?? string.Empty;
            MarkerText = item?.PlatformMarker;
            Points = item?.GlyphPoints;
            HasGlyph = item?.HasGlyph ?? false;
            HasMarker = item?.HasMarker ?? false;
        }
    }
}
