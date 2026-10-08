using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Media;

namespace Augram.App.Components.Fields.Text;

public sealed class TextFieldRenderer : IFieldRenderer
{
    /// <summary>The line break a multi-line editor inserts, whatever the platform's own.</summary>
    public const string LineBreak = "\n";

    public string Kind => "Text";

    public Control Build(Field field)
    {
        var text = (TextField)field;
        var editor = new TextBox { IsReadOnly = text.Value.IsReadOnly };
        editor.Classes.Add("field-editor");
        if (text.Multiline)
        {
            // Behaviour here, look (minimum and maximum height, scrolling) in the theme's TextBox.field-editor.multiline.
            editor.AcceptsReturn = true;
            editor.NewLine = LineBreak;
            editor.TextWrapping = TextWrapping.Wrap;
            editor.Classes.Add("multiline");
        }

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
