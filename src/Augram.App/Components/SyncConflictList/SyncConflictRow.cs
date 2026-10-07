using Augram.Core.Gestures;
using Augram.Core.Sync;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.SyncConflictList;

/// <summary>
/// Lookless row of the sync conflict list (F8 sync): the item's name and detail line, this machine's version and the
/// other machine's side by side (a gesture as two glyphs, row class <c>row-glyph</c>, so what differs is visible;
/// anything else as a one-line summary, or "deleted"), and the choice dropdown <c>PART_Choice</c> offering the
/// entry's choices. Picking one sets <see cref="SelectedChoice"/> and raises <see cref="ChoiceChanged"/>; the frame
/// is the shared row frame (row rule), so every list row looks the same.
/// </summary>
public sealed class SyncConflictRow : TemplatedControl
{
    public static readonly StyledProperty<SyncConflictEntry?> EntryProperty =
        AvaloniaProperty.Register<SyncConflictRow, SyncConflictEntry?>(nameof(Entry));

    public static readonly StyledProperty<SyncChoice> SelectedChoiceProperty =
        AvaloniaProperty.Register<SyncConflictRow, SyncChoice>(nameof(SelectedChoice));

    public static readonly StyledProperty<string> NameTextProperty =
        AvaloniaProperty.Register<SyncConflictRow, string>(nameof(NameText), string.Empty);

    public static readonly StyledProperty<string> DetailTextProperty =
        AvaloniaProperty.Register<SyncConflictRow, string>(nameof(DetailText), string.Empty);

    public static readonly StyledProperty<string> MineTextProperty =
        AvaloniaProperty.Register<SyncConflictRow, string>(nameof(MineText), string.Empty);

    public static readonly StyledProperty<string> TheirsTextProperty =
        AvaloniaProperty.Register<SyncConflictRow, string>(nameof(TheirsText), string.Empty);

    public static readonly StyledProperty<IReadOnlyList<GesturePoint>?> MinePointsProperty =
        AvaloniaProperty.Register<SyncConflictRow, IReadOnlyList<GesturePoint>?>(nameof(MinePoints));

    public static readonly StyledProperty<IReadOnlyList<GesturePoint>?> TheirsPointsProperty =
        AvaloniaProperty.Register<SyncConflictRow, IReadOnlyList<GesturePoint>?>(nameof(TheirsPoints));

    public static readonly StyledProperty<bool> HasMineGlyphProperty =
        AvaloniaProperty.Register<SyncConflictRow, bool>(nameof(HasMineGlyph));

    public static readonly StyledProperty<bool> HasTheirsGlyphProperty =
        AvaloniaProperty.Register<SyncConflictRow, bool>(nameof(HasTheirsGlyph));

    private ComboBox? _choice;
    private SyncConflictEntry? _labelsFor;
    private bool _applying;

    public event EventHandler<SyncChoice>? ChoiceChanged;

    public SyncConflictEntry? Entry
    {
        get => GetValue(EntryProperty);
        set => SetValue(EntryProperty, value);
    }

    /// <summary>The choice the dropdown shows; starts as the entry's <see cref="SyncConflictEntry.Selected"/>.</summary>
    public SyncChoice SelectedChoice
    {
        get => GetValue(SelectedChoiceProperty);
        set => SetValue(SelectedChoiceProperty, value);
    }

    public string NameText
    {
        get => GetValue(NameTextProperty);
        private set => SetValue(NameTextProperty, value);
    }

    public string DetailText
    {
        get => GetValue(DetailTextProperty);
        private set => SetValue(DetailTextProperty, value);
    }

    public string MineText
    {
        get => GetValue(MineTextProperty);
        private set => SetValue(MineTextProperty, value);
    }

    public string TheirsText
    {
        get => GetValue(TheirsTextProperty);
        private set => SetValue(TheirsTextProperty, value);
    }

    public IReadOnlyList<GesturePoint>? MinePoints
    {
        get => GetValue(MinePointsProperty);
        private set => SetValue(MinePointsProperty, value);
    }

    public IReadOnlyList<GesturePoint>? TheirsPoints
    {
        get => GetValue(TheirsPointsProperty);
        private set => SetValue(TheirsPointsProperty, value);
    }

    public bool HasMineGlyph
    {
        get => GetValue(HasMineGlyphProperty);
        private set => SetValue(HasMineGlyphProperty, value);
    }

    public bool HasTheirsGlyph
    {
        get => GetValue(HasTheirsGlyphProperty);
        private set => SetValue(HasTheirsGlyphProperty, value);
    }

    /// <summary>The labels the dropdown offers, in the entry's order.</summary>
    public IReadOnlyList<string> ChoiceLabels => Entry?.Choices.Select(choice => choice.Label).ToList() ?? [];

    /// <summary>What the dropdown does: selects <paramref name="choice"/> when the entry offers it and says so; false otherwise.</summary>
    public bool Choose(SyncChoice choice)
    {
        if (Entry is not { } entry || !entry.Choices.Any(offered => offered.Value == choice))
        {
            ApplyChoice();
            return false;
        }

        if (choice != SelectedChoice)
        {
            SelectedChoice = choice;
            ChoiceChanged?.Invoke(this, choice);
        }

        return true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _choice = e.NameScope.Find<ComboBox>("PART_Choice");
        _labelsFor = null;
        if (_choice is not null)
        {
            _choice.SelectionChanged += (_, _) =>
            {
                if (!_applying && Entry is { } entry && _choice.SelectedIndex >= 0 && _choice.SelectedIndex < entry.Choices.Count)
                {
                    Choose(entry.Choices[_choice.SelectedIndex].Value);
                }
            };
        }

        ApplyChoice();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EntryProperty)
        {
            var entry = Entry;
            NameText = entry?.Name ?? string.Empty;
            DetailText = entry?.Detail ?? string.Empty;
            MineText = entry?.Mine.Text ?? string.Empty;
            TheirsText = entry?.Theirs.Text ?? string.Empty;
            MinePoints = entry?.Mine.Points;
            TheirsPoints = entry?.Theirs.Points;
            HasMineGlyph = entry?.Mine.HasGlyph == true;
            HasTheirsGlyph = entry?.Theirs.HasGlyph == true;
            if (entry is not null)
            {
                SelectedChoice = entry.Selected;
            }

            ApplyChoice();
        }
        else if (change.Property == SelectedChoiceProperty)
        {
            ApplyChoice();
        }
    }

    private void ApplyChoice()
    {
        if (_choice is null)
        {
            return;
        }

        _applying = true;
        try
        {
            var entry = Entry;
            if (!ReferenceEquals(_labelsFor, entry))
            {
                _labelsFor = entry;
                _choice.ItemsSource = ChoiceLabels;
            }

            var choices = entry?.Choices ?? [];
            _choice.SelectedIndex = choices.ToList().FindIndex(choice => choice.Value == SelectedChoice);
        }
        finally
        {
            _applying = false;
        }
    }
}
