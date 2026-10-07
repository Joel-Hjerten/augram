using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.Toggles;

/// <summary>A <see cref="TogglesField"/>: its options' check boxes in one horizontal row, each two-way on its own binding.</summary>
public sealed class TogglesFieldRenderer : IFieldRenderer
{
    public string Kind => "Toggles";

    public Control Build(Field field)
    {
        var toggles = (TogglesField)field;
        var editor = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        editor.Classes.Add("field-editor");
        foreach (var option in toggles.Options)
        {
            var box = new CheckBox { Content = option.Caption, IsEnabled = !option.Value.IsReadOnly };
            box.Classes.Add("toggle-option");
            BindingObserver.Attach(box, option.Value, value => box.IsChecked = value);
            box.IsCheckedChanged += (_, _) => option.Value.Set(box.IsChecked == true);
            editor.Children.Add(box);
        }

        return editor;
    }
}
