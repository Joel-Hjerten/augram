using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless header row of a section in the <see cref="CommandTree"/> (F5a): expander
/// (<c>PART_Expander</c>, raising <see cref="ExpandToggled"/>), name, command count and, when the item
/// allows it, the active toggle (<see cref="CanToggleActive"/>). Marked <c>:collapsed</c> while its
/// commands are hidden, <c>:pinned</c> for a section that can be neither renamed nor deleted
/// (Uncategorized), <c>:elsewhere</c> for a group or category used only on the other platform (F8) and <c>:nested</c> for a
/// section inside another one (a hold remap in its app group, indented as a command is). What the header offers comes from
/// the <see cref="SectionItem"/>, never from the tab.
/// </summary>
[PseudoClasses(":collapsed", ":pinned", ":elsewhere", ":nested")]
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

    public SectionRow()
    {
        Tapped += OnHeaderTapped;
    }

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

    /// <summary>
    /// A tap anywhere on the header toggles it, not only on the expander (Joel, 2026-10-07); the row is selected as
    /// well, as a click on any row is. A tap that lands on a button (the expander raises its own toggle, the active
    /// box is a toggle button) or in the name editor belongs to that control. The second click of a double click
    /// toggles nothing: a double click renames (<c>CommandTree</c>), and the tree puts back the first click's toggle.
    /// </summary>
    private void OnHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (!e.Handled && LastPressClickCount < 2 && !IsFromOwnControl(e.Source as Visual))
        {
            ExpandToggled?.Invoke(this, EventArgs.Empty);
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
            PseudoClasses.Set(":elsewhere", item is { IsElsewhere: true });
            PseudoClasses.Set(":nested", item is { IsNested: true });
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(":collapsed", !IsExpanded);
        }
    }
}
