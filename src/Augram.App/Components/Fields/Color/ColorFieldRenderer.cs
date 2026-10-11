using Augram.App.Declarations;
using Augram.Core.Config;
using Avalonia.Controls;

namespace Augram.App.Components.Fields.Color;

/// <summary>A <see cref="ColorEditor"/>; with the field's presets it is the swatch row and Custom… (plan 0006 decision 8).</summary>
public sealed class ColorFieldRenderer : IFieldRenderer
{
    public string Kind => "Color";

    public Control Build(Field field)
    {
        var colour = (ColorField)field;
        var editor = new ColorEditor { IsEnabled = !colour.Value.IsReadOnly, Presets = colour.Presets };
        // One change for all three channels: setting them one by one wrote half-updated colours to the settings.
        BindingObserver.Attach(editor, colour.Value, value => editor.SetRgb(value.R, value.G, value.B));
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
