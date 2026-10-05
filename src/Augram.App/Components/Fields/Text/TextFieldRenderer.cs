using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.Text;

public sealed class TextFieldRenderer : IFieldRenderer
{
    public string Kind => "Text";

    public Control Build(Field field)
    {
        var text = (TextField)field;
        var editor = new TextBox { IsReadOnly = text.Value.IsReadOnly };
        editor.Classes.Add("field-editor");
        BindingObserver.Attach(editor, text.Value, value =>
        {
            if (editor.Text != value)
            {
                editor.Text = value;
            }
        });
        // Observe the property, not TextChanged: the event fires for typing only, the property for every change.
        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                text.Value.Set(editor.Text ?? string.Empty);
            }
        };
        return editor;
    }
}
