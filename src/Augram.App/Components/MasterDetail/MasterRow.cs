using Augram.App.Components.CommandTree;
using Avalonia;

namespace Augram.App.Components.MasterDetail;

/// <summary>
/// Lookless row of a <see cref="MasterDetail"/> list: name (renamed in place through <see cref="ItemRow"/>'s editor), the
/// item's summary under it and the active box, in the list row metrics every list shares (<c>Border.row</c>,
/// <c>Grid.row-line</c>). Greyed via <c>:inactive</c>. Raises <see cref="ItemRow.RenameCommitted"/> and
/// <see cref="ItemRow.ActiveToggled"/>; never touches a store.
/// </summary>
public sealed class MasterRow : ItemRow
{
    public static readonly StyledProperty<MasterItem?> ItemProperty =
        AvaloniaProperty.Register<MasterRow, MasterItem?>(nameof(Item));

    public static readonly StyledProperty<string> SummaryTextProperty =
        AvaloniaProperty.Register<MasterRow, string>(nameof(SummaryText), string.Empty);

    public MasterItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public string SummaryText
    {
        get => GetValue(SummaryTextProperty);
        private set => SetValue(SummaryTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            var item = Item;
            NameText = item?.Name ?? string.Empty;
            IsActive = item?.IsActive ?? true;
            SummaryText = item?.Summary ?? string.Empty;
        }
    }
}
