using Augram.App.Declarations;
using Augram.Core.Config;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.Color;

public sealed class ColorFieldRenderer : IFieldRenderer
{
    public string Kind => "Color";

    public Control Build(Field field)
    {
        var colour = (ColorField)field;
        var editor = new ColorEditor { IsEnabled = !colour.Value.IsReadOnly };
        BindingObserver.Attach(editor, colour.Value, value =>
        {
            editor.Red = value.R;
            editor.Green = value.G;
            editor.Blue = value.B;
        });
        editor.ColorChanged += (_, _) =>
        {
            var next = new RgbColor((byte)editor.Red, (byte)editor.Green, (byte)editor.Blue);
            if (next != colour.Value.Get())
            {
                colour.Value.Set(next);
            }
        };
        return editor;
    }
}
