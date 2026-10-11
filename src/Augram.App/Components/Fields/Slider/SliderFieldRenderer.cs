using System.Globalization;
using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Augram.App.Components.Fields.Slider;

/// <summary>
/// A <see cref="Avalonia.Controls.Slider"/> and the value beside it with its unit ("62%"), on one line (<c>slider-line</c>;
/// the theme sizes the <c>field-slider</c> and the <c>slider-value</c>). Moving the slider writes every step through the
/// binding, so a setting previews live while it is dragged; showing the form writes nothing back.
/// </summary>
public sealed class SliderFieldRenderer : IFieldRenderer
{
    public string Kind => "Slider";

    /// <summary>The text beside the slider: the number without trailing zeros, then the unit ("62%", "12 px", "0.5").</summary>
    public static string ValueText(double value, string unit) => value.ToString("0.###", CultureInfo.InvariantCulture) + unit;

    public Control Build(Field field)
    {
        var declared = (SliderField)field;
        var slider = new Avalonia.Controls.Slider
        {
            Minimum = declared.Min,
            Maximum = declared.Max,
            SmallChange = declared.Step,
            LargeChange = declared.Step * 10,
            TickFrequency = declared.Step,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        slider.Classes.Add("field-slider");
        var value = new TextBlock();
        value.Classes.Add("slider-value");
        var line = new StackPanel { Orientation = Orientation.Horizontal, IsEnabled = !declared.Value.IsReadOnly, Children = { slider, value } };
        line.Classes.Add("slider-line");

        var applying = false;
        BindingObserver.Attach(line, declared.Value, current =>
        {
            applying = true;
            try
            {
                slider.Value = current;
            }
            finally
            {
                applying = false;
            }

            value.Text = ValueText(current, declared.Unit);
        });
        slider.ValueChanged += (_, e) =>
        {
            // A value from the binding (clamped into range by the slider, perhaps) is shown, never written back.
            if (applying)
            {
                return;
            }

            value.Text = ValueText(e.NewValue, declared.Unit);
            if (e.NewValue != declared.Value.Get())
            {
                declared.Value.Set(e.NewValue);
            }
        };
        return line;
    }
}
