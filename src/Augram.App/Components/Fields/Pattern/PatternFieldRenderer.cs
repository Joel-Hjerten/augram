using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.Pattern;

/// <summary>
/// A <see cref="PatternField"/>: text box, magnifier, "Use Regex" in one line (<c>Grid.pattern-line</c>; spacing is the
/// theme's). The text and the toggle are two-way on their own bindings; the placeholder follows its binding.
/// </summary>
public sealed class PatternFieldRenderer : IFieldRenderer
{
    public string Kind => "Pattern";

    public Control Build(Field field)
    {
        var pattern = (PatternField)field;
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        line.Classes.Add("pattern-line");

        var editor = new TextBox { IsReadOnly = pattern.Value.IsReadOnly };
        editor.Classes.Add("field-editor");
        editor.Classes.Add("wide");
        BindingObserver.Attach(editor, pattern.Value, value =>
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
                pattern.Value.Set(editor.Text ?? string.Empty);
            }
        };
        if (pattern.Placeholder is { } placeholder)
        {
            BindingObserver.Attach(editor, placeholder, value => editor.Watermark = value);
        }

        line.Children.Add(editor);

        if (pattern.Finder?.Invoke() is { } finder)
        {
            finder.Classes.Add("pattern-finder");
            if (pattern.FinderVisible is { } visible)
            {
                BindingObserver.Attach(finder, visible, value => finder.IsVisible = value);
            }

            Grid.SetColumn(finder, 1);
            line.Children.Add(finder);
        }

        var regex = new CheckBox { Content = PatternField.UseRegexCaption, IsEnabled = !pattern.IsRegex.IsReadOnly };
        regex.Classes.Add("toggle-option");
        regex.Classes.Add("pattern-regex");
        BindingObserver.Attach(regex, pattern.IsRegex, value => regex.IsChecked = value);
        regex.IsCheckedChanged += (_, _) => pattern.IsRegex.Set(regex.IsChecked == true);
        Grid.SetColumn(regex, 2);
        line.Children.Add(regex);
        return line;
    }
}
