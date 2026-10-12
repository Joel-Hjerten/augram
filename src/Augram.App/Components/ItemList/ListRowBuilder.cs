using Augram.App.Components.Fields;
using Augram.App.Components.Parts;
using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Augram.App.Components.ItemList;

/// <summary>
/// Builds the pieces of an <see cref="ItemList"/> from its spec: toolbar (filters, the search box, then actions),
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

        if (spec.Search is { } search)
        {
            items.Add(BuildSearch(search, refresh));
        }

        foreach (var action in spec.Toolbar ?? [])
        {
            items.Add(ToolbarButton.Create(action.Label, () => action.Execute(selected())));
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
            var column = spec.Columns[i];
            Control cell;
            if (column.Component is not null)
            {
                cell = column.Component(item);
            }
            else
            {
                cell = new TextBlock { Text = column.Cell(item), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            }

            cell.Classes.Add("cell");
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return grid;
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

    /// <summary>The spec's search box (plan 0009), kept in step with its binding both ways; every change refreshes the rows.</summary>
    private static Control BuildSearch(ListSearch search, Action refresh)
    {
        var box = new SearchBox.SearchBox();
        BindingObserver.Attach(box, search.Text, value =>
        {
            if (box.Text != value)
            {
                box.Text = value;
            }
        });
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == SearchBox.SearchBox.TextProperty)
            {
                search.Text.Set(box.Text);
                refresh();
            }
        };
        return box;
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
