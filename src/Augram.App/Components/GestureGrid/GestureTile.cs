using Augram.App.Components.ListEditing;
using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace Augram.App.Components.GestureGrid;

/// <summary>
/// Lookless tile of the gesture grid: glyph above name, greyed via the <c>:inactive</c> pseudo-class,
/// outlined via <c>:duplicate</c> when the item has duplicates (A7), and marked <c>:partner</c> with the
/// score while the selected tile is a duplicate of it.
/// Rename edits in place through <see cref="ItemRow"/> (F5a), the one rename editor: <see cref="ItemRow.BeginEdit"/> shows the
/// template's <c>PART_NameEditor</c> with the current name; Enter or leaving the editor raises <see cref="ItemRow.RenameCommitted"/>,
/// Escape reverts. A tile has no active box (its template has no <c>PART_Active</c>), and can be renamed only once it has an
/// item. The tile never touches a store; the grid forwards the commit to its host.
/// </summary>
[PseudoClasses(":duplicate", ":partner")]
public sealed class GestureTile : ItemRow
{
    public static readonly StyledProperty<GestureTileItem?> ItemProperty =
        AvaloniaProperty.Register<GestureTile, GestureTileItem?>(nameof(Item));

    public static readonly StyledProperty<IReadOnlyList<GesturePoint>?> PointsProperty =
        AvaloniaProperty.Register<GestureTile, IReadOnlyList<GesturePoint>?>(nameof(Points));

    public static readonly StyledProperty<bool> HasDuplicatesProperty =
        AvaloniaProperty.Register<GestureTile, bool>(nameof(HasDuplicates));

    public static readonly StyledProperty<string?> PartnerScoreTextProperty =
        AvaloniaProperty.Register<GestureTile, string?>(nameof(PartnerScoreText));

    public GestureTile()
    {
        CanRename = false;
    }

    public GestureTileItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public IReadOnlyList<GesturePoint>? Points
    {
        get => GetValue(PointsProperty);
        private set => SetValue(PointsProperty, value);
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
            CanRename = item is not null;
        }
        else if (change.Property == HasDuplicatesProperty)
        {
            PseudoClasses.Set(":duplicate", HasDuplicates);
        }
        else if (change.Property == PartnerScoreTextProperty)
        {
            PseudoClasses.Set(":partner", PartnerScoreText is not null);
        }
    }
}
