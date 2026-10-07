using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Augram.App.Components.GestureGrid;

/// <summary>
/// Lookless tile of the gesture grid: glyph above name, greyed via the <c>:inactive</c> pseudo-class,
/// outlined via <c>:duplicate</c> when the item has duplicates (A7), and marked <c>:partner</c> with the
/// score while the selected tile is a duplicate of it.
/// Rename edits in place (F5a): <see cref="BeginEdit"/> shows the template's <c>PART_NameEditor</c>
/// with the current name; Enter raises <see cref="RenameCommitted"/>, Escape reverts. The tile
/// never touches a store; the grid forwards the commit to its host.
/// </summary>
[PseudoClasses(":inactive", ":editing", ":duplicate", ":partner")]
public sealed class GestureTile : TemplatedControl
{
    public static readonly StyledProperty<GestureTileItem?> ItemProperty =
        AvaloniaProperty.Register<GestureTile, GestureTileItem?>(nameof(Item));

    public static readonly StyledProperty<string> NameTextProperty =
        AvaloniaProperty.Register<GestureTile, string>(nameof(NameText), string.Empty);

    public static readonly StyledProperty<IReadOnlyList<GesturePoint>?> PointsProperty =
        AvaloniaProperty.Register<GestureTile, IReadOnlyList<GesturePoint>?>(nameof(Points));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<GestureTile, bool>(nameof(IsActive), true);

    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<GestureTile, bool>(nameof(IsEditing));

    public static readonly StyledProperty<bool> HasDuplicatesProperty =
        AvaloniaProperty.Register<GestureTile, bool>(nameof(HasDuplicates));

    public static readonly StyledProperty<string?> PartnerScoreTextProperty =
        AvaloniaProperty.Register<GestureTile, string?>(nameof(PartnerScoreText));

    private TextBox? _editor;

    /// <summary>Raised with the trimmed new name when the user presses Enter in the editor.</summary>
    public event EventHandler<string>? RenameCommitted;

    public GestureTileItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public string NameText
    {
        get => GetValue(NameTextProperty);
        private set => SetValue(NameTextProperty, value);
    }

    public IReadOnlyList<GesturePoint>? Points
    {
        get => GetValue(PointsProperty);
        private set => SetValue(PointsProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        private set => SetValue(IsActiveProperty, value);
    }

    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        private set => SetValue(IsEditingProperty, value);
    }

    public bool HasDuplicates
    {
        get => GetValue(HasDuplicatesProperty);
        private set => SetValue(HasDuplicatesProperty, value);
    }

    /// <summary>"97%" while the selected tile is likely to be confused with this one; null otherwise.</summary>
    public string? PartnerScoreText
    {
        get => GetValue(PartnerScoreTextProperty);
        private set => SetValue(PartnerScoreTextProperty, value);
    }

    /// <summary>Marks or clears this tile as a partner of the selected one.</summary>
    public void SetPartner(double? score)
        => PartnerScoreText = score is { } value ? Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%" : null;

    public void BeginEdit()
    {
        if (Item is null)
        {
            return;
        }

        IsEditing = true;
        if (_editor is not null)
        {
            _editor.Text = Item.Name;
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
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            var item = Item;
            NameText = item?.Name ?? string.Empty;
            Points = item?.Points;
            IsActive = item?.IsActive ?? true;
            HasDuplicates = item?.HasDuplicates ?? false;
        }
        else if (change.Property == HasDuplicatesProperty)
        {
            PseudoClasses.Set(":duplicate", HasDuplicates);
        }
        else if (change.Property == PartnerScoreTextProperty)
        {
            PseudoClasses.Set(":partner", PartnerScoreText is not null);
        }
        else if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(":inactive", !IsActive);
        }
        else if (change.Property == IsEditingProperty)
        {
            PseudoClasses.Set(":editing", IsEditing);
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
