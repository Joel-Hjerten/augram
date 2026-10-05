using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Augram.App.Components.Fields.Color;

/// <summary>
/// Lookless RGB editor: three channels in, a <see cref="Swatch"/> out. The template supplies
/// <c>PART_Red</c>, <c>PART_Green</c> and <c>PART_Blue</c> (<see cref="NumericUpDown"/>) and may bind
/// <see cref="Swatch"/> to a preview surface. <see cref="ColorChanged"/> fires once per channel change.
/// </summary>
public sealed class ColorEditor : TemplatedControl
{
    public static readonly StyledProperty<int> RedProperty = AvaloniaProperty.Register<ColorEditor, int>(nameof(Red));
    public static readonly StyledProperty<int> GreenProperty = AvaloniaProperty.Register<ColorEditor, int>(nameof(Green));
    public static readonly StyledProperty<int> BlueProperty = AvaloniaProperty.Register<ColorEditor, int>(nameof(Blue));
    public static readonly StyledProperty<IBrush?> SwatchProperty = AvaloniaProperty.Register<ColorEditor, IBrush?>(nameof(Swatch));

    private NumericUpDown? _red;
    private NumericUpDown? _green;
    private NumericUpDown? _blue;

    public ColorEditor()
    {
        UpdateSwatch();
    }

    public event EventHandler? ColorChanged;

    public int Red
    {
        get => GetValue(RedProperty);
        set => SetValue(RedProperty, Math.Clamp(value, 0, 255));
    }

    public int Green
    {
        get => GetValue(GreenProperty);
        set => SetValue(GreenProperty, Math.Clamp(value, 0, 255));
    }

    public int Blue
    {
        get => GetValue(BlueProperty);
        set => SetValue(BlueProperty, Math.Clamp(value, 0, 255));
    }

    public IBrush? Swatch
    {
        get => GetValue(SwatchProperty);
        private set => SetValue(SwatchProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _red = Wire(e, "PART_Red", () => Red, value => Red = value);
        _green = Wire(e, "PART_Green", () => Green, value => Green = value);
        _blue = Wire(e, "PART_Blue", () => Blue, value => Blue = value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RedProperty || change.Property == GreenProperty || change.Property == BlueProperty)
        {
            Push(_red, Red);
            Push(_green, Green);
            Push(_blue, Blue);
            UpdateSwatch();
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static NumericUpDown? Wire(TemplateAppliedEventArgs e, string part, Func<int> get, Action<int> set)
    {
        var editor = e.NameScope.Find<NumericUpDown>(part);
        if (editor is null)
        {
            return null;
        }

        editor.Value = get();
        editor.ValueChanged += (_, args) =>
        {
            if (args.NewValue is { } value)
            {
                set((int)value);
            }
        };
        return editor;
    }

    private static void Push(NumericUpDown? editor, int value)
    {
        if (editor is not null && editor.Value != value)
        {
            editor.Value = value;
        }
    }

    private void UpdateSwatch() => Swatch = new SolidColorBrush(Avalonia.Media.Color.FromRgb((byte)Red, (byte)Green, (byte)Blue));
}
