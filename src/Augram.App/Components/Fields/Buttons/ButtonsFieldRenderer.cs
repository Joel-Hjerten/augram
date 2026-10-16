using Augram.App.Components.Parts;
using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Augram.App.Components.Fields.Buttons;

/// <summary>
/// Renders a <see cref="ButtonsField"/>: toolbar buttons, and beside them a status line (<c>DockPanel.field-line</c>, the note
/// role, wrapping), a hint (the help role, on the buttons' line with its gap), or, without either, the buttons side by side after an
/// optional dropdown (<c>StackPanel.field-buttons</c>). The three lines are the ones Options and the import review had laid by hand.
/// </summary>
public sealed class ButtonsFieldRenderer : IFieldRenderer
{
    public string Kind => "Buttons";

    public Control Build(Field field)
    {
        var buttons = (ButtonsField)field;
        if (buttons.Status is { } status)
        {
            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            text.Classes.Add("note");
            BindingObserver.Attach(text, status, value => text.Text = value);
            var line = new DockPanel();
            foreach (var button in buttons.Buttons.Select(Button))
            {
                DockPanel.SetDock(button, Dock.Left);
                line.Children.Add(button);
            }

            line.Children.Add(text);
            line.Classes.Add("field-line");
            return line;
        }

        if (buttons.Hint is { } hint)
        {
            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            text.Classes.Add("help");
            BindingObserver.Attach(text, hint, value => text.Text = value);
            var line = new StackPanel();
            line.Children.AddRange(buttons.Buttons.Select(Button));
            line.Children.Add(text);
            // The buttons line's gap keeps the hint off the button (Joel, 2026-10-16: "More options…" touched its hint).
            line.Classes.Add("field-buttons");
            return line;
        }

        var row = new StackPanel();
        if (buttons.Choice is { } choice)
        {
            var dropdown = new ComboBox { ItemsSource = choice.Labels, SelectedIndex = choice.SelectedIndex };
            dropdown.Classes.Add("field-editor");
            dropdown.SelectionChanged += (_, _) =>
            {
                if (dropdown.SelectedIndex >= 0)
                {
                    choice.SelectedIndex = dropdown.SelectedIndex;
                }
            };
            row.Children.Add(dropdown);
        }

        row.Children.AddRange(buttons.Buttons.Select(Button));
        row.Classes.Add("field-buttons");
        return row;
    }

    private static Button Button(FieldButton declared)
    {
        var button = ToolbarButton.Create(declared.Label, declared.Click);
        if (declared.Enabled is { } enabled)
        {
            BindingObserver.Attach(button, enabled, value => button.IsEnabled = value);
        }

        return button;
    }
}
