using Augram.App.Components.Fields;
using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;

namespace Augram.App.Components.ItemList;

/// <summary>
/// Builds the pieces of an <see cref="ItemList"/> from its spec: toolbar (filters, then actions),
/// header row, data rows (text cells or the spec's row component), context menu and key bindings.
/// Visual choices (padding, fonts) come from the theme through the classes set here.
/// </summary>
internal static class ListRowBuilder
{
    public static IReadOnlyList<Control> BuildToolbar(ListSpec spec, Action refresh, Func<object?> selected)
    {
        var items = new List<Control>();
        foreach (var filter in spec.Filters ?? [])
        {
            items.Add(BuildFilter(filter, refresh));
        }

        foreach (var action in spec.Toolbar ?? [])
        {
            var button = new Button { Content = action.Label };
            button.Classes.Add("toolbar");
            button.Click += (_, _) => action.Execute(selected());
            items.Add(button);
        }

        return items;
    }

    public static Control BuildHeader(ListSpec spec)
    {
        var grid = NewGrid(spec);
        for (var i = 0; i < spec.Columns.Count; i++)
        {
            var cell = new TextBlock { Text = spec.Columns[i].Title };
            cell.Classes.Add("column-header");
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        grid.Classes.Add("list-header");
        return grid;
    }

    public static Control BuildRow(ListSpec spec, object item)
    {
        if (spec.RowComponent is not null)
        {
            return spec.RowComponent(item);
        }

        var grid = NewGrid(spec);
        for (var i = 0; i < spec.Columns.Count; i++)
        {
            var cell = new TextBlock { Text = spec.Columns[i].Cell(item), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            cell.Classes.Add("cell");
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
    }

    public static void AttachActions(ListBox rows, ListSpec spec, Func<object?> selected)
    {
        if (spec.ContextMenu is { Count: > 0 } menuActions)
        {
            var menu = new ContextMenu();
            foreach (var action in menuActions)
            {
                var item = new MenuItem { Header = action.Label };
                item.Click += (_, _) => action.Execute(selected());
                menu.Items.Add(item);
            }

            rows.ContextMenu = menu;
        }

        foreach (var key in spec.Keymap ?? [])
        {
            rows.KeyBindings.Add(new KeyBinding
            {
                Gesture = KeyGesture.Parse(key.Gesture),
                Command = new RelayCommand(() => key.Action.Execute(selected())),
            });
        }
    }

    private static Grid NewGrid(ListSpec spec)
    {
        var grid = new Grid();
        foreach (var column in spec.Columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(column.Width > 0 ? new GridLength(column.Width) : GridLength.Star));
        }

        grid.Classes.Add("list-row");
        return grid;
    }

    private static Control BuildFilter(ListFilter filter, Action refresh)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Classes.Add("list-filter");
        var label = new TextBlock { Text = filter.Label, VerticalAlignment = VerticalAlignment.Center };
        label.Classes.Add("field-label");
        var combo = new ComboBox { ItemsSource = filter.Options };
        combo.Classes.Add("field-editor");
        BindingObserver.Attach(combo, filter.Selected, value => combo.SelectedIndex = Math.Max(0, filter.Options.ToList().IndexOf(value)));
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < filter.Options.Count)
            {
                filter.Selected.Set(filter.Options[combo.SelectedIndex]);
                refresh();
            }
        };
        panel.Children.Add(label);
        panel.Children.Add(combo);
        return panel;
    }
}
