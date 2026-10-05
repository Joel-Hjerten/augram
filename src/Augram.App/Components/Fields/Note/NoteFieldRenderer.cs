using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Media;

namespace Augram.App.Components.Fields.Note;

public sealed class NoteFieldRenderer : IFieldRenderer
{
    public string Kind => "Note";

    public Control Build(Field field)
    {
        var note = (NoteField)field;
        var editor = new TextBlock { TextWrapping = TextWrapping.Wrap };
        editor.Classes.Add("note");
        BindingObserver.Attach(editor, note.Text, value => editor.Text = value);
        return editor;
    }
}
