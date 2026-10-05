using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.Dropdown;

public sealed class DropdownFieldRenderer : IFieldRenderer
{
    public string Kind => "Dropdown";

    public Control Build(Field field)
    {
        // DropdownField<T> is generic; its non-generic face carries labels and a selected index.
        var choices = ((IChoiceSource)field).AsChoices();
        var editor = new ComboBox { ItemsSource = choices.Labels, IsEnabled = !choices.IsReadOnly };
        editor.Classes.Add("field-editor");
        BindingObserver.Attach(editor, field.Binding!, () => editor.SelectedIndex = choices.SelectedIndex);
        editor.SelectionChanged += (_, _) =>
        {
            if (editor.SelectedIndex >= 0)
            {
                choices.SelectedIndex = editor.SelectedIndex;
            }
        };
        return editor;
    }
}
