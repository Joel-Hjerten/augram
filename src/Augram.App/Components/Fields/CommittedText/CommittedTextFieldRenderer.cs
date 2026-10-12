using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Augram.App.Components.Fields.CommittedText;

/// <summary>
/// Renders a <see cref="CommittedTextField"/>: a text box that reports every keystroke as a draft and commits on leaving or
/// Enter (Escape reverts), with the draft's problem under it (<c>TextBlock.error</c>, shown only while there is one).
/// </summary>
public sealed class CommittedTextFieldRenderer : IFieldRenderer
{
    public string Kind => "CommittedText";

    public Control Build(Field field)
    {
        var committed = (CommittedTextField)field;
        var editor = new TextBox();
        editor.Classes.Add("field-editor");
        if (committed.Wide)
        {
            editor.Classes.Add("wide");
        }

        BindingObserver.Attach(editor, committed.Text, value =>
        {
            if (editor.Text != value)
            {
                editor.Text = value;
            }
        });
        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                committed.Text.Set(editor.Text ?? string.Empty);
            }
        };
        editor.LostFocus += (_, _) => committed.Commit();
        editor.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                committed.Commit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                committed.Revert();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        var error = new TextBlock();
        error.Classes.Add("error");
        BindingObserver.Attach(error, committed.Problem, value =>
        {
            error.Text = value;
            error.IsVisible = value.Length > 0;
        });
        return new StackPanel { Children = { editor, error } };
    }
}
