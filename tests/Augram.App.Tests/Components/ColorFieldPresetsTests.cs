using Augram.App.Components.Fields.Color;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.Tests.Support;
using Augram.Core.Config;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>
/// A colour field with presets (plan 0006 decision 8): round swatches in their order, the current colour ringed, a colour
/// that is no preset as one more swatch at the end, Custom… opening the same picker; without presets, the editor as before.
/// </summary>
public sealed class ColorFieldPresetsTests
{
    private static readonly RgbColor Green = new(0x00, 0xFF, 0x40);
    private static readonly RgbColor Yellow = new(0xF5, 0xC5, 0x42);

    [Fact]
    public void TheRainbowIsSevenNamedColoursInOrder()
    {
        Assert.Equal(
            [("Red", "#FF5A4F"), ("Orange", "#FF8A3D"), ("Yellow", "#F5C542"), ("Green", "#00FF40"), ("Cyan", "#36D6E7"), ("Blue", "#5B9BFF"), ("Purple", "#A78BFA")],
            ColourPresets.Rainbow.Select(preset => (preset.Name, preset.Colour.ToString())));
    }

    [AvaloniaFact]
    public void PresetsShowAsSwatchesInOrder_TheCurrentColourRinged_ThenCustom()
    {
        var vm = new FakeOptions { Colour = Green };
        var editor = Show(vm, ColourPresets.Rainbow);

        Assert.Contains(":presets", editor.Classes);
        var swatches = Swatches(editor);
        Assert.Equal(["Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple"], swatches.Where(button => button.IsVisible).Select(button => (string)ToolTip.GetTip(button)!));
        Assert.Equal(["Green"], Selected(editor));
        Assert.False(swatches[^1].IsVisible);
        Assert.Equal(Avalonia.Media.Color.FromRgb(0xF5, 0xC5, 0x42), Fill(swatches[2]));
        Assert.All(swatches, button => Assert.Contains("swatch", button.Classes));

        // After the swatches, Custom… and no channel boxes: the row is the swatches.
        var custom = editor.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Custom");
        Assert.Equal("Custom…", custom.Content);
        Assert.Empty(editor.GetVisualDescendants().OfType<NumericUpDown>());
        Assert.Empty(editor.GetVisualDescendants().OfType<ColorPicker>());
    }

    [AvaloniaFact]
    public void ClickingASwatchSetsItsColour_AsOneChange_AndMovesTheRing()
    {
        var vm = new FakeOptions { Colour = Green };
        var editor = Show(vm, ColourPresets.Rainbow);
        var changes = new List<RgbColor>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FakeOptions.Colour))
            {
                changes.Add(vm.Colour);
            }
        };

        Click(Swatches(editor)[2]);

        Assert.Equal([Yellow], changes);
        Assert.Equal(["Yellow"], Selected(editor));

        // A colour from outside moves the ring too.
        vm.Colour = new RgbColor(0x5B, 0x9B, 0xFF);
        Assert.Equal(["Blue"], Selected(editor));
    }

    [AvaloniaFact]
    public void AColourThatIsNoPreset_IsOneMoreSwatchAtTheEnd_Selected_UntilAPresetIsPicked()
    {
        var custom = new RgbColor(140, 90, 60);
        var vm = new FakeOptions { Colour = custom };
        var editor = Show(vm, ColourPresets.Rainbow);
        var swatches = Swatches(editor);

        Assert.Equal(8, swatches.Count);
        Assert.True(swatches[^1].IsVisible);
        Assert.Equal(ColorEditor.CustomSwatchName, ToolTip.GetTip(swatches[^1]));
        Assert.Equal([ColorEditor.CustomSwatchName], Selected(editor));
        Assert.Equal(Avalonia.Media.Color.FromRgb(140, 90, 60), Fill(swatches[^1]));

        // Another custom colour: the same extra swatch, in the new colour.
        vm.Colour = new RgbColor(1, 2, 3);
        Assert.Equal(Avalonia.Media.Color.FromRgb(1, 2, 3), Fill(swatches[^1]));

        Click(swatches[0]);

        Assert.Equal(new RgbColor(0xFF, 0x5A, 0x4F), vm.Colour);
        Assert.False(swatches[^1].IsVisible);
        Assert.Equal(["Red"], Selected(editor));
    }

    [AvaloniaFact]
    public void CustomOpensTheSamePicker_WhoseColourIsWrittenAsOneChange()
    {
        var vm = new FakeOptions { Colour = Green };
        var editor = Show(vm, ColourPresets.Rainbow);
        var custom = editor.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_Custom");
        var flyout = Assert.IsType<Flyout>(custom.Flyout);
        var picker = Assert.IsAssignableFrom<ColorView>(flyout.Content);
        var changes = 0;
        vm.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(FakeOptions.Colour) ? 1 : 0;

        Assert.Same(picker, editor.Picker);
        Assert.Equal("PART_Picker", picker.Name);
        Assert.False(picker.IsAlphaEnabled);
        Assert.Equal(Avalonia.Media.Color.FromRgb(0, 255, 64), picker.Color);

        flyout.ShowAt(custom);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Laid out as the plain field's picker is (ColorPickerLayoutTests): no padding, the brightness slider inside the popup.
        Assert.True(flyout.IsOpen);
        var presenter = picker.GetVisualAncestors().OfType<FlyoutPresenter>().Single();
        Assert.Equal(default, presenter.Padding);
        Assert.All(presenter.GetVisualDescendants().OfType<TabItem>().Where(tab => tab.IsVisible), tab => Assert.Equal(48, tab.Bounds.Height));
        var slider = picker.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "ColorSpectrumThirdComponentSlider");
        var origin = slider.TranslatePoint(new Point(0, 0), presenter)!.Value;
        Assert.True(origin.X >= 0 && origin.X + slider.Bounds.Width <= presenter.Bounds.Width, $"slider at {origin.X} in a {presenter.Bounds.Width} px popup");

        picker.Color = Avalonia.Media.Color.FromRgb(10, 20, 30);
        flyout.Hide();

        Assert.Equal(new RgbColor(10, 20, 30), vm.Colour);
        Assert.Equal(1, changes);
        Assert.Equal([ColorEditor.CustomSwatchName], Selected(editor));
    }

    [AvaloniaFact]
    public void WithoutPresets_TheEditorIsAsBefore()
    {
        foreach (var presets in new IReadOnlyList<ColourPreset>?[] { null, [] })
        {
            var vm = new FakeOptions { Colour = Green };
            var editor = Show(vm, presets);

            Assert.DoesNotContain(":presets", editor.Classes);
            Assert.Empty(editor.SwatchButtons);
            Assert.DoesNotContain(editor.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("swatch") || button.Name == "PART_Custom");
            Assert.IsType<ColorPicker>(editor.Picker);
            Assert.Equal(3, editor.GetVisualDescendants().OfType<NumericUpDown>().Count(box => box.Classes.Contains("channel")));
        }
    }

    private static ColorEditor Show(FakeOptions vm, IReadOnlyList<ColourPreset>? presets)
    {
        var field = new ColorField("Colour", new DelegateBinding<RgbColor>(() => vm.Colour, v => vm.Colour = v, vm)) { Presets = presets };
        var form = new SectionForm { Screen = new FormScreen("Test", [new Section("Trail", [field])]) };
        new Window { Content = form, Width = 900, Height = 600 }.Show();
        return Assert.IsType<ColorEditor>(form.GetVisualDescendants().OfType<FieldRow>().Single().Editor);
    }

    private static List<Button> Swatches(ColorEditor editor) => [.. editor.SwatchButtons.Cast<Button>()];

    private static List<string> Selected(ColorEditor editor) =>
        [.. Swatches(editor).Where(button => button.IsVisible && button.Classes.Contains("selected")).Select(button => (string)ToolTip.GetTip(button)!)];

    private static Avalonia.Media.Color Fill(Button swatch) => ((ISolidColorBrush)((Border)swatch.Content!).Background!).Color;

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
