using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Augram.App.Components.Fields.CheckList;

/// <summary>
/// A <see cref="CheckListField"/>: its items' check boxes one under the other inside a framed list that scrolls past the
/// theme's height (<c>Border.check-list</c>), each two-way on its own binding, its detail in the <c>help</c> style after
/// the caption.
/// </summary>
public sealed class CheckListFieldRenderer : IFieldRenderer
{
    public string Kind => "CheckList";

    public Control Build(Field field)
    {
        var list = (CheckListField)field;
        var items = new StackPanel();
        items.Classes.Add("check-list-items");
        foreach (var item in list.Items)
        {
            items.Children.Add(Box(item));
        }

        var frame = new Border { Child = new ScrollViewer { Content = items } };
        frame.Classes.Add("field-editor");
        frame.Classes.Add("check-list");
        return frame;
    }

    private static CheckBox Box(CheckListItem item)
    {
        var caption = new StackPanel { Orientation = Orientation.Horizontal };
        caption.Children.Add(new TextBlock { Text = item.Caption, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrEmpty(item.Detail))
        {
            var detail = new TextBlock { Text = item.Detail, VerticalAlignment = VerticalAlignment.Center };
            detail.Classes.Add("help");
            detail.Classes.Add("check-list-detail");
            caption.Children.Add(detail);
        }

        var box = new CheckBox { Content = caption, IsEnabled = !item.Value.IsReadOnly };
        box.Classes.Add("check-list-option");
        BindingObserver.Attach(box, item.Value, value => box.IsChecked = value);
        box.IsCheckedChanged += (_, _) => item.Value.Set(box.IsChecked == true);
        return box;
    }
}
