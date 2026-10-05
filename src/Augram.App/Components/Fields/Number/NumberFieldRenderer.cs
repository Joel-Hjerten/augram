using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.Number;

public sealed class NumberFieldRenderer : IFieldRenderer
{
    public string Kind => "Number";

    public Control Build(Field field)
    {
        var number = (NumberField)field;
        var editor = new NumericUpDown
        {
            Minimum = (decimal)number.Min,
            Maximum = (decimal)number.Max,
            Increment = (decimal)number.Step,
            FormatString = "0.###",
            IsEnabled = !number.Value.IsReadOnly,
        };
        editor.Classes.Add("field-editor");
        BindingObserver.Attach(editor, number.Value, value => editor.Value = (decimal)value);
        editor.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } value)
            {
                number.Value.Set((double)value);
            }
        };
        return editor;
    }
}
