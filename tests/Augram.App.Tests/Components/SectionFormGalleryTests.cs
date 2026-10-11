#if DEBUG
using Augram.App.Components.Fields.Color;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>The gallery's SectionForm page (gallery rule) shows the slider, enabled and disabled, and the colour field with presets: a preset selected, and a custom colour.</summary>
public sealed class SectionFormGalleryTests
{
    [AvaloniaFact]
    public void TheSectionFormPageShowsTheSliderAndTheColourSwatches()
    {
        var screen = Assert.IsType<FormScreen>(GalleryNavigation.Entry().SubEntries!.Single(entry => entry.Key == GalleryNavigation.Key + ".sectionform").Screen!());
        var form = new SectionForm { Screen = screen };
        new Window { Content = form, Width = 1100, Height = 1400 }.Show();
        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToDictionary(row => row.Label);

        Assert.True(rows["Slider"].Editor!.IsEnabled);
        var gated = rows["Slider, enabled by Toggle"].Editor!;
        Assert.True(gated.IsEnabled);
        ((CheckBox)rows["Toggle"].Editor!).IsChecked = false;
        Assert.False(gated.IsEnabled);

        Assert.Equal(["Green"], Selected((ColorEditor)rows["Color with presets"].Editor!));
        Assert.Equal([ColorEditor.CustomSwatchName], Selected((ColorEditor)rows["Presets, custom colour"].Editor!));
        Assert.Empty(((ColorEditor)rows["Color"].Editor!).SwatchButtons);
    }

    private static List<string> Selected(ColorEditor editor) =>
        [.. editor.SwatchButtons.Where(button => button.IsVisible && button.Classes.Contains("selected")).Select(button => (string)ToolTip.GetTip(button)!)];
}
#endif
