using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless header row of a section in the <see cref="CommandTree"/> (F5a): expander
/// (<c>PART_Expander</c>, raising <see cref="ExpandToggled"/>), name, command count and, when the item
/// allows it, the active toggle (<see cref="CanToggleActive"/>). Marked <c>:collapsed</c> while its
/// commands are hidden and <c>:pinned</c> for a section that can be neither renamed nor deleted
/// (Uncategorized). What the header offers comes from the <see cref="SectionItem"/>, never from the tab.
/// </summary>
[PseudoClasses(":collapsed", ":pinned")]
public sealed class SectionRow : ItemRow
{
    public static readonly StyledProperty<SectionItem?> ItemProperty =
        AvaloniaProperty.Register<SectionRow, SectionItem?>(nameof(Item));

    public static readonly StyledProperty<string> CountTextProperty =
        AvaloniaProperty.Register<SectionRow, string>(nameof(CountText), string.Empty);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SectionRow, bool>(nameof(IsExpanded), true);

    public static readonly StyledProperty<bool> CanToggleActiveProperty =
        AvaloniaProperty.Register<SectionRow, bool>(nameof(CanToggleActive));

    public event EventHandler? ExpandToggled;

    public SectionItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    /// <summary>"3 commands", "1 command", "no commands".</summary>
    public string CountText
    {
        get => GetValue(CountTextProperty);
        private set => SetValue(CountTextProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        private set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Shows the active check box; an app group has one, a category does not.</summary>
    public bool CanToggleActive
    {
        get => GetValue(CanToggleActiveProperty);
        private set => SetValue(CanToggleActiveProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_Expander") is { } expander)
        {
            expander.Click += (_, _) => ExpandToggled?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            var item = Item;
            NameText = item?.Name ?? string.Empty;
            IsActive = item?.IsActive ?? true;
            IsExpanded = item?.IsExpanded ?? true;
            CanRename = item?.CanRename ?? false;
            CanToggleActive = item?.CanToggleActive ?? false;
            CountText = item?.CountText ?? string.Empty;
            PseudoClasses.Set(":pinned", item is { CanRename: false, CanDelete: false });
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(":collapsed", !IsExpanded);
        }
    }
}
