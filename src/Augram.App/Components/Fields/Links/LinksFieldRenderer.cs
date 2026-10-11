using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Augram.App.Components.Fields.Links;

/// <summary>
/// A <see cref="LinksField"/>: one <c>Button.link</c> per link, one under the other (its detail in the <c>help</c> style after
/// it), rebuilt whenever the binding changes; a link without an action is a plain <c>note</c>, and an empty list shows the
/// field's empty text.
/// </summary>
public sealed class LinksFieldRenderer : IFieldRenderer
{
    public string Kind => "Links";

    public Control Build(Field field)
    {
        var links = (LinksField)field;
        var panel = new StackPanel();
        panel.Classes.Add("links");
        BindingObserver.Attach(panel, links.Links, items => Fill(panel, items, links.EmptyText));
        return panel;
    }

    private static void Fill(StackPanel panel, IReadOnlyList<LinkItem> items, string emptyText)
    {
        panel.Children.Clear();
        if (items.Count == 0)
        {
            // An empty placeholder ("none"): the Secondary text role (Themes/Default/Text.axaml).
            var empty = new TextBlock { Text = emptyText, VerticalAlignment = VerticalAlignment.Center };
            empty.Classes.Add("secondary");
            panel.Children.Add(empty);
            return;
        }

        foreach (var item in items)
        {
            panel.Children.Add(Line(item));
        }
    }

    private static Control Line(LinkItem item)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        if (item.Open is { } open)
        {
            var link = new Button { Content = item.Caption };
            link.Classes.Add("link");
            link.Click += (_, _) => open();
            line.Children.Add(link);
        }
        else
        {
            var text = new TextBlock { Text = item.Caption, VerticalAlignment = VerticalAlignment.Center };
            text.Classes.Add("note");
            line.Children.Add(text);
        }

        if (!string.IsNullOrEmpty(item.Detail))
        {
            var detail = new TextBlock { Text = item.Detail, VerticalAlignment = VerticalAlignment.Center };
            detail.Classes.Add("help");
            detail.Classes.Add("link-detail");
            line.Children.Add(detail);
        }

        return line;
    }
}
