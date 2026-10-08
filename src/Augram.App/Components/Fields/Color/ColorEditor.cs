using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Augram.App.Components.Fields.Color;

/// <summary>
/// Lookless RGB editor: three channels in, a <see cref="Swatch"/> out. The template supplies
/// <c>PART_Red</c>, <c>PART_Green</c> and <c>PART_Blue</c> (<see cref="NumericUpDown"/>) and may bind
/// <see cref="Swatch"/> to a preview surface. A <c>PART_Swatch</c> button opens Avalonia's colour picker (spectrum, sliders,
/// hex; no alpha, opacity is a setting of its own) in a flyout (Joel, 2026-10-08). <see cref="ColorChanged"/> fires once
/// per colour change: <see cref="SetRgb"/> moves all three channels as one, so setting a colour from outside or dragging in
/// the picker never reports a half-updated colour (setting the channels one by one wrote red-with-the-old-green-and-blue
/// to the settings before the real colour).
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
    private ColorView? _picker;
    private bool _batching;

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

    /// <summary>The picker behind the swatch, once the template is applied; null without a <c>PART_Swatch</c> button.</summary>
    public ColorView? Picker => _picker;

    /// <summary>Sets all three channels as one change: <see cref="ColorChanged"/> fires once, and not at all when nothing changed.</summary>
    public void SetRgb(int red, int green, int blue)
    {
        if (Red == red && Green == green && Blue == blue)
        {
            return;
        }

        _batching = true;
        try
        {
            Red = red;
            Green = green;
            Blue = blue;
        }
        finally
        {
            _batching = false;
        }

        Changed();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _red = Wire(e, "PART_Red", () => Red, value => Red = value);
        _green = Wire(e, "PART_Green", () => Green, value => Green = value);
        _blue = Wire(e, "PART_Blue", () => Blue, value => Blue = value);
        _picker = AttachPicker(e.NameScope.Find<Button>("PART_Swatch"));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_batching && (change.Property == RedProperty || change.Property == GreenProperty || change.Property == BlueProperty))
        {
            Changed();
        }
    }

    private void Changed()
    {
        Push(_red, Red);
        Push(_green, Green);
        Push(_blue, Blue);
        if (_picker is not null && _picker.Color != CurrentColor)
        {
            _picker.Color = CurrentColor;
        }

        UpdateSwatch();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private Avalonia.Media.Color CurrentColor => Avalonia.Media.Color.FromRgb((byte)Red, (byte)Green, (byte)Blue);

    /// <summary>The flyout with the picker, made here rather than in the template so it works with any theme's swatch button.</summary>
    private ColorView? AttachPicker(Button? swatch)
    {
        if (swatch is null)
        {
            return null;
        }

        var picker = new ColorView
        {
            Color = CurrentColor,
            IsAlphaEnabled = false,
            IsAlphaVisible = false,
            IsColorPaletteVisible = false,
            Width = 300,
        };
        picker.ColorChanged += (_, args) => SetRgb(args.NewColor.R, args.NewColor.G, args.NewColor.B);
        swatch.Flyout = new Flyout { Content = picker, Placement = PlacementMode.Bottom };
        return picker;
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

    private void UpdateSwatch() => Swatch = new SolidColorBrush(CurrentColor);
}
