using Augram.App.Inspector;
using Augram.App.Navigation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Augram.App.Components.Shell;

/// <summary>
/// Renders a <see cref="NavigationRegistry"/> as top-level tabs with optional sub-tabs (F7). Each
/// leaf entry becomes a <see cref="ScreenHost.ScreenHost"/>; the registry is the only input, so adding a
/// tab never touches this class. <see cref="SelectedKey"/> selects a tab by key (e.g. <c>--gallery</c>).
/// </summary>
public sealed class Shell : TemplatedControl
{
    public static readonly StyledProperty<NavigationRegistry?> RegistryProperty =
        AvaloniaProperty.Register<Shell, NavigationRegistry?>(nameof(Registry));

    public static readonly StyledProperty<IReadOnlyList<TabItem>> TabsProperty =
        AvaloniaProperty.Register<Shell, IReadOnlyList<TabItem>>(nameof(Tabs), []);

    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<Shell, string?>(nameof(SelectedKey));

    private TabControl? _tabs;

    public NavigationRegistry? Registry
    {
        get => GetValue(RegistryProperty);
        set => SetValue(RegistryProperty, value);
    }

    public IReadOnlyList<TabItem> Tabs
    {
        get => GetValue(TabsProperty);
        private set => SetValue(TabsProperty, value);
    }

    public string? SelectedKey
    {
        get => GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _tabs = e.NameScope.Find<TabControl>("PART_Tabs");
        ApplySelection();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RegistryProperty)
        {
            Tabs = Registry is null ? [] : [.. Registry.Entries.Select(entry => BuildTab(entry, string.Empty))];
            ApplySelection();
        }
        else if (change.Property == SelectedKeyProperty)
        {
            ApplySelection();
        }
    }

    private static TabItem BuildTab(NavEntry entry, string parentPath)
    {
        var path = parentPath.Length == 0 ? entry.Title : $"{parentPath} › {entry.Title}";
        Control content;
        if (entry.HasSubEntries)
        {
            var sub = new TabControl { ItemsSource = entry.SubEntries!.Select(child => BuildTab(child, path)).ToList() };
            sub.Classes.Add("sub");
            content = sub;
        }
        else if (entry.Screen is not null)
        {
            content = new ScreenHost.ScreenHost { Screen = entry.Screen() };
        }
        else
        {
            content = new TextPanel.TextPanel { Text = "Nothing declared for this tab." };
        }

        Region.Mark(content, entry.Title, new RegionInfo(path, "Tab", default));
        return new TabItem { Header = entry.Title, Content = content, Tag = entry.Key };
    }

    private void ApplySelection()
    {
        if (_tabs is null || SelectedKey is null)
        {
            return;
        }

        foreach (var tab in Tabs)
        {
            if (Equals(tab.Tag, SelectedKey))
            {
                _tabs.SelectedItem = tab;
                return;
            }

            if (tab.Content is TabControl sub && sub.ItemsSource is IEnumerable<TabItem> children)
            {
                var child = children.FirstOrDefault(item => Equals(item.Tag, SelectedKey));
                if (child is not null)
                {
                    _tabs.SelectedItem = tab;
                    sub.SelectedItem = child;
                    return;
                }
            }
        }
    }
}
