using Augram.App.Declarations;
using Augram.Core.Config;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Augram.App.Components.Fields.Color;

/// <summary>
/// Lookless RGB editor: three channels in, a <see cref="Swatch"/> out. The template supplies
/// <c>PART_Red</c>, <c>PART_Green</c> and <c>PART_Blue</c> (<see cref="NumericUpDown"/>) and may bind
/// <see cref="Swatch"/> to a preview surface. A <c>PART_Picker</c> (<see cref="ColorView"/>; the Wireframe theme uses
/// Avalonia's <see cref="ColorPicker"/>, a swatch button whose flyout is laid out by its own theme) picks the colour
/// with a spectrum, sliders and hex; no alpha, opacity is a setting of its own (Joel, 2026-10-08). <see cref="ColorChanged"/> fires once
/// per colour change: <see cref="SetRgb"/> moves all three channels as one, so setting a colour from outside or dragging in
/// the picker never reports a half-updated colour (setting the channels one by one wrote red-with-the-old-green-and-blue
/// to the settings before the real colour).
/// <para>
/// With <see cref="Presets"/> (plan 0006 decision 8) the editor has the <c>:presets</c> pseudo-class, and the theme gives it
/// another template: <see cref="SwatchButtons"/> in a row, then a Custom… button whose flyout holds the <c>PART_Picker</c>.
/// Each swatch is a <see cref="Button"/> with the <c>swatch</c> class around a <c>swatch-fill</c> border in its colour; the
/// one equal to the current colour also has <c>selected</c> (the theme rings it). A current colour that is no preset shows
/// as one more swatch at the end, selected; it goes again once a preset is picked. A click on a swatch sets its colour.
/// </para>
/// </summary>
public sealed class ColorEditor : TemplatedControl
{
    public static readonly StyledProperty<int> RedProperty = AvaloniaProperty.Register<ColorEditor, int>(nameof(Red));
    public static readonly StyledProperty<int> GreenProperty = AvaloniaProperty.Register<ColorEditor, int>(nameof(Green));
    public static readonly StyledProperty<int> BlueProperty = AvaloniaProperty.Register<ColorEditor, int>(nameof(Blue));
    public static readonly StyledProperty<IBrush?> SwatchProperty = AvaloniaProperty.Register<ColorEditor, IBrush?>(nameof(Swatch));

    public static readonly StyledProperty<IReadOnlyList<ColourPreset>?> PresetsProperty =
        AvaloniaProperty.Register<ColorEditor, IReadOnlyList<ColourPreset>?>(nameof(Presets));

    public static readonly StyledProperty<IReadOnlyList<Control>> SwatchButtonsProperty =
        AvaloniaProperty.Register<ColorEditor, IReadOnlyList<Control>>(nameof(SwatchButtons), []);

    /// <summary>The tooltip of the extra swatch a colour that is no preset gets.</summary>
    public const string CustomSwatchName = "Custom colour";

    private NumericUpDown? _red;
    private NumericUpDown? _green;
    private NumericUpDown? _blue;
    private ColorView? _picker;
    private bool _batching;
    private Button? _customSwatch;

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

    /// <summary>The swatches to offer, in order; null or empty for the plain editor (swatch, picker and channels).</summary>
    public IReadOnlyList<ColourPreset>? Presets
    {
        get => GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    /// <summary>One button per preset, then the custom colour's (shown only while the colour is no preset); empty without presets. The template lists them.</summary>
    public IReadOnlyList<Control> SwatchButtons
    {
        get => GetValue(SwatchButtonsProperty);
        private set => SetValue(SwatchButtonsProperty, value);
    }

    /// <summary>The template's picker, once applied; null in a template without <c>PART_Picker</c>.</summary>
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
        _picker = WirePicker(e.NameScope.Find<ColorView>("PART_Picker"));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_batching && (change.Property == RedProperty || change.Property == GreenProperty || change.Property == BlueProperty))
        {
            Changed();
        }
        else if (change.Property == PresetsProperty)
        {
            BuildSwatches();
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
        UpdateSelection();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private Avalonia.Media.Color CurrentColor => Avalonia.Media.Color.FromRgb((byte)Red, (byte)Green, (byte)Blue);

    private ColorView? WirePicker(ColorView? picker)
    {
        if (picker is null)
        {
            return null;
        }

        picker.Color = CurrentColor;
        picker.ColorChanged += (_, args) => SetRgb(args.NewColor.R, args.NewColor.G, args.NewColor.B);
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

    /// <summary>One button per preset and the custom colour's, and the pseudo-class that picks the template.</summary>
    private void BuildSwatches()
    {
        var presets = Presets ?? [];
        PseudoClasses.Set(":presets", presets.Count > 0);
        if (presets.Count == 0)
        {
            _customSwatch = null;
            SwatchButtons = [];
            return;
        }

        var buttons = new List<Control>(presets.Count + 1);
        foreach (var preset in presets)
        {
            var colour = preset.Colour;
            var button = SwatchButton(preset.Name, colour);
            button.Tag = colour;
            button.Click += (_, _) => SetRgb(colour.R, colour.G, colour.B);
            buttons.Add(button);
        }

        // It always shows the current colour, so a click on it changes nothing; it is a button only to look and focus like the rest.
        _customSwatch = SwatchButton(CustomSwatchName, Current);
        buttons.Add(_customSwatch);
        SwatchButtons = buttons;
        UpdateSelection();
    }

    /// <summary>Rings the preset equal to the current colour, or shows the custom colour's swatch, ringed, when none is.</summary>
    private void UpdateSelection()
    {
        if (_customSwatch is null)
        {
            return;
        }

        var current = Current;
        var matched = false;
        foreach (var button in SwatchButtons)
        {
            if (button.Tag is RgbColor colour)
            {
                var selected = colour == current;
                button.Classes.Set("selected", selected);
                matched |= selected;
            }
        }

        _customSwatch.IsVisible = !matched;
        _customSwatch.Classes.Set("selected", !matched);
        if (_customSwatch.Content is Border fill)
        {
            fill.Background = new SolidColorBrush(CurrentColor);
        }
    }

    private RgbColor Current => new((byte)Red, (byte)Green, (byte)Blue);

    private static Button SwatchButton(string name, RgbColor colour)
    {
        var fill = new Border { Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(colour.R, colour.G, colour.B)) };
        fill.Classes.Add("swatch-fill");
        var button = new Button { Content = fill };
        button.Classes.Add("swatch");
        ToolTip.SetTip(button, name);
        AutomationProperties.SetName(button, name);
        return button;
    }
}
