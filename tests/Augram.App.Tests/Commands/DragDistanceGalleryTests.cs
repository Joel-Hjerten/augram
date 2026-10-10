#if DEBUG
using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>The gallery's Drag distance, Not in page (gallery rule, plan 0004 step 3): every state of both rows, and the dialog.</summary>
public sealed class DragDistanceGalleryTests
{
    [AvaloniaFact]
    public void ThePageShowsEachStateOfBothRows_AndTheNotInDialog()
    {
        var form = new SectionForm { Screen = Assert.IsType<FormScreen>(CommandGalleryPages.DragDistanceNotInPage()) };
        new Window { Content = form, Width = 1000, Height = 1400 }.Show();

        var headers = form.GetVisualDescendants().OfType<CommandHeader>().ToList();
        Assert.Equal(["Zoom in", "Zoom out", "Zoom in", "Volume up"], headers.Select(header => header.NameText));
        Assert.Equal([true, true, true, false], headers.Select(header => header.ShowsDragDistance));
        Assert.Equal([true, false, false, false], headers.Select(header => header.HasOwnDragDistance));
        Assert.Equal([true, true, false, true], headers.Select(header => header.CanSetNotIn));
        Assert.Equal(["Photoshop, Steam games", CommandItem.NoneNotIn], headers.Take(2).Select(header => header.NotInText));
        Assert.Equal("Options value (12 px)", Assert.IsAssignableFrom<IEnumerable<string>>(headers[1].GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == "PART_DragDistanceMode").ItemsSource).First());
        Assert.Equal(3, headers[0].GetVisualDescendants().OfType<NumericUpDown>().Single().Value);

        var dialog = Assert.Single(form.GetVisualDescendants().OfType<FormDialog>());
        var boxes = dialog.GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(4, boxes.Count);
        Assert.Equal([false, false, true, true], boxes.Select(box => box.IsChecked == true));
    }
}
#endif
