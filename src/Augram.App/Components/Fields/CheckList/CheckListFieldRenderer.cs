using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Augram.App.Components.Fields.CheckList;

/// <summary>
/// A <see cref="CheckListField"/>: its items' check boxes one under the other inside a framed list that scrolls past the
/// theme's height (<c>Border.check-list</c>), each two-way on its own binding, its detail in the <c>help</c> style after
/// the caption. A list with <see cref="CheckListField.LiveItems"/> is rebuilt from it whenever it changes. An item with
/// <see cref="CheckListItem.Remove"/> sits in a <c>check-list-row</c> with an ✕ (<c>Button.check-list-remove</c>) at its end;
/// while it is removable the caption is <c>muted</c>, its note shows and the ✕ with it.
/// </summary>
public sealed class CheckListFieldRenderer : IFieldRenderer
{
    public string Kind => "CheckList";

    public Control Build(Field field)
    {
        var list = (CheckListField)field;
        var items = new StackPanel();
        items.Classes.Add("check-list-items");
        if (list.LiveItems is { } live)
        {
            BindingObserver.Attach(items, live, current => Fill(items, current));
        }
        else
        {
            Fill(items, list.Items);
        }

        var frame = new Border { Child = new ScrollViewer { Content = items } };
        frame.Classes.Add("field-editor");
        frame.Classes.Add("check-list");
        return frame;
    }

    private static void Fill(StackPanel items, IReadOnlyList<CheckListItem> current)
    {
        items.Children.Clear();
        foreach (var item in current)
        {
            items.Children.Add(item.Remove is { } remove ? Row(item, remove) : Box(item, out _));
        }
    }

    private static CheckBox Box(CheckListItem item, out StackPanel caption)
    {
        caption = new StackPanel { Orientation = Orientation.Horizontal };
        caption.Children.Add(new TextBlock { Text = item.Caption, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrEmpty(item.Detail))
        {
            caption.Children.Add(Detail(item.Detail));
        }

        var box = new CheckBox { Content = caption, IsEnabled = !item.Value.IsReadOnly };
        box.Classes.Add("check-list-option");
        BindingObserver.Attach(box, item.Value, value => box.IsChecked = value);
        box.IsCheckedChanged += (_, _) => item.Value.Set(box.IsChecked == true);
        return box;
    }

    /// <summary>The box, then the ✕ at the row's end; the removable look follows <see cref="CheckListRemove.Removable"/>.</summary>
    private static DockPanel Row(CheckListItem item, CheckListRemove remove)
    {
        var box = Box(item, out var caption);
        var name = (TextBlock)caption.Children[0];
        var note = Detail(remove.Note);
        caption.Children.Add(note);

        var button = new Button { Content = "✕" };
        button.Classes.Add("check-list-remove");
        ToolTip.SetTip(button, remove.Tip);
        button.Click += (_, _) => remove.Click();
        DockPanel.SetDock(button, Dock.Right);

        var row = new DockPanel { Children = { button, box } };
        row.Classes.Add("check-list-row");
        BindingObserver.Attach(row, remove.Removable, removable =>
        {
            name.Classes.Set("muted", removable);
            note.IsVisible = removable;
            button.IsVisible = removable;
        });
        return row;
    }

    private static TextBlock Detail(string text)
    {
        var detail = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        detail.Classes.Add("help");
        detail.Classes.Add("check-list-detail");
        return detail;
    }
}
