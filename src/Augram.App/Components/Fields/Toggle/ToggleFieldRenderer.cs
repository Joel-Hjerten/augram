using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.Toggle;

public sealed class ToggleFieldRenderer : IFieldRenderer
{
    public string Kind => "Toggle";

    public Control Build(Field field)
    {
        var toggle = (ToggleField)field;
        var editor = new CheckBox { IsEnabled = !toggle.Value.IsReadOnly };
        editor.Classes.Add("field-editor");
        BindingObserver.Attach(editor, toggle.Value, value => editor.IsChecked = value);
        editor.IsCheckedChanged += (_, _) => toggle.Value.Set(editor.IsChecked == true);
        return editor;
    }
}
