using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.ButtonRadio;

public sealed class ButtonRadioFieldRenderer : IFieldRenderer
{
    public string Kind => "ButtonRadio";

    public Control Build(Field field)
    {
        var choices = ((IChoiceSource)field).AsChoices();
        var group = "radio-" + Guid.NewGuid().ToString("N");
        var editor = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        editor.Classes.Add("field-editor");
        var buttons = new List<RadioButton>();
        for (var i = 0; i < choices.Labels.Count; i++)
        {
            var index = i;
            var button = new RadioButton { Content = choices.Labels[i], GroupName = group, IsEnabled = !choices.IsReadOnly };
            button.Classes.Add("button-radio");
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true)
                {
                    choices.SelectedIndex = index;
                }
            };
            buttons.Add(button);
            editor.Children.Add(button);
        }

        BindingObserver.Attach(editor, field.Binding!, () =>
        {
            var selected = choices.SelectedIndex;
            for (var i = 0; i < buttons.Count; i++)
            {
                buttons[i].IsChecked = i == selected;
            }
        });
        return editor;
    }
}
