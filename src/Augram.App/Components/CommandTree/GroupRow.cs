using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Lookless header row of an app group in the <see cref="CommandTree"/> (F5a): expander
/// (<c>PART_Expander</c>, raising <see cref="ExpandToggled"/>), name, command count, active toggle.
/// Marked <c>:global</c> for the pinned Global group (which cannot be renamed) and <c>:collapsed</c>
/// while its commands are hidden.
/// </summary>
[PseudoClasses(":global", ":collapsed")]
public sealed class GroupRow : ItemRow
{
    public static readonly StyledProperty<GroupItem?> ItemProperty =
        AvaloniaProperty.Register<GroupRow, GroupItem?>(nameof(Item));

    public static readonly StyledProperty<string> CountTextProperty =
        AvaloniaProperty.Register<GroupRow, string>(nameof(CountText), string.Empty);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<GroupRow, bool>(nameof(IsExpanded), true);

    public static readonly StyledProperty<bool> IsGlobalProperty =
        AvaloniaProperty.Register<GroupRow, bool>(nameof(IsGlobal));

    public event EventHandler? ExpandToggled;

    public GroupItem? Item
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

    public bool IsGlobal
    {
        get => GetValue(IsGlobalProperty);
        private set => SetValue(IsGlobalProperty, value);
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
            IsGlobal = item?.IsGlobal ?? false;
            CanRename = item is { IsGlobal: false };
            CountText = item is null ? string.Empty : item.Commands.Count switch
            {
                0 => "no commands",
                1 => "1 command",
                var n => n.ToString(CultureInfo.InvariantCulture) + " commands",
            };
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(":collapsed", !IsExpanded);
        }
        else if (change.Property == IsGlobalProperty)
        {
            PseudoClasses.Set(":global", IsGlobal);
        }
    }
}
