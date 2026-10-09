using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless command row of the <see cref="CommandTree"/> (F5a): the gesture's glyph, or a trigger
/// badge ("Wheel up", "No trigger") when there is no glyph to draw, the name (editable in place), the
/// category tag when the item has one (an app group with categories), the step summary, the F8
/// platform marker when the command has one, and the active toggle. Marked <c>:elsewhere</c> for a command used only on the
/// other platform (F8), listed while the list shows other platforms.
/// </summary>
[PseudoClasses(":elsewhere")]
public sealed class CommandRow : ItemRow
{
    public static readonly StyledProperty<CommandItem?> ItemProperty =
        AvaloniaProperty.Register<CommandRow, CommandItem?>(nameof(Item));

    public static readonly StyledProperty<string> TriggerTextProperty =
        AvaloniaProperty.Register<CommandRow, string>(nameof(TriggerText), string.Empty);

    public static readonly StyledProperty<string> BadgeTextProperty =
        AvaloniaProperty.Register<CommandRow, string>(nameof(BadgeText), string.Empty);

    public static readonly StyledProperty<string> SummaryTextProperty =
        AvaloniaProperty.Register<CommandRow, string>(nameof(SummaryText), string.Empty);

    public static readonly StyledProperty<string?> MarkerTextProperty =
        AvaloniaProperty.Register<CommandRow, string?>(nameof(MarkerText));

    public static readonly StyledProperty<string?> CategoryTextProperty =
        AvaloniaProperty.Register<CommandRow, string?>(nameof(CategoryText));

    public static readonly StyledProperty<IReadOnlyList<GesturePoint>?> PointsProperty =
        AvaloniaProperty.Register<CommandRow, IReadOnlyList<GesturePoint>?>(nameof(Points));

    public static readonly StyledProperty<bool> HasGlyphProperty =
        AvaloniaProperty.Register<CommandRow, bool>(nameof(HasGlyph));

    public static readonly StyledProperty<bool> HasMarkerProperty =
        AvaloniaProperty.Register<CommandRow, bool>(nameof(HasMarker));

    public static readonly StyledProperty<bool> HasCategoryProperty =
        AvaloniaProperty.Register<CommandRow, bool>(nameof(HasCategory));

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

    /// <summary>The badge's text when there is no glyph: <see cref="TriggerText"/> one word per line ("Wheel" / "up"), so it never breaks inside a word.</summary>
    public string BadgeText
    {
        get => GetValue(BadgeTextProperty);
        private set => SetValue(BadgeTextProperty, value);
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

    /// <summary>The category tag ("General"); null when the item has none.</summary>
    public string? CategoryText
    {
        get => GetValue(CategoryTextProperty);
        private set => SetValue(CategoryTextProperty, value);
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

    public bool HasCategory
    {
        get => GetValue(HasCategoryProperty);
        private set => SetValue(HasCategoryProperty, value);
    }

    /// <summary>One word per line, a combination's "+" kept at the end of the word before it: "Right +" / "wheel" / "up".</summary>
    public static string BadgeLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new List<string>();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word == "+" && lines.Count > 0)
            {
                lines[^1] += " +";
            }
            else
            {
                lines.Add(word);
            }
        }

        return string.Join('\n', lines);
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
            BadgeText = BadgeLines(TriggerText);
            SummaryText = item?.StepSummary ?? string.Empty;
            MarkerText = item?.PlatformMarker;
            PseudoClasses.Set(":elsewhere", item is { IsElsewhere: true });
            CategoryText = item?.CategoryLabel;
            Points = item?.GlyphPoints;
            HasGlyph = item?.HasGlyph ?? false;
            HasMarker = item?.HasMarker ?? false;
            HasCategory = item?.HasCategoryLabel ?? false;
        }
    }
}
