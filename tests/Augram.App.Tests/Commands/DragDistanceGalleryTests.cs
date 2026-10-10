#if DEBUG
using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.SectionForm;
using Augram.App.Components.WindowFinder;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Augram.App.ViewModels.Commands;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>The gallery's Drag distance, Not in page (gallery rule, plan 0004 steps 3 and 7): every state of both rows, and the dialog with and without Per command entries.</summary>
public sealed class DragDistanceGalleryTests
{
    [AvaloniaFact]
    public void ThePageShowsEachStateOfBothRows_AndTheNotInDialog()
    {
        var form = new SectionForm { Screen = Assert.IsType<FormScreen>(CommandGalleryPages.DragDistanceNotInPage()) };
        new Window { Content = form, Width = 1000, Height = 1800 }.Show();

        var headers = form.GetVisualDescendants().OfType<CommandHeader>().ToList();
        Assert.Equal(["Zoom in", "Zoom out", "Zoom in", "Volume up"], headers.Select(header => header.NameText));
        Assert.Equal([true, true, true, false], headers.Select(header => header.ShowsDragDistance));
        Assert.Equal([true, false, false, false], headers.Select(header => header.HasOwnDragDistance));
        Assert.Equal([true, true, true, true], headers.Select(header => header.CanSetNotIn));
        Assert.Equal(["Eyeris, Spine", CommandItem.NoneNotIn, "Eyeris"], headers.Take(3).Select(header => header.NotInText));
        Assert.Equal("Options value (12 px)", Assert.IsAssignableFrom<IEnumerable<string>>(headers[1].GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == "PART_DragDistanceMode").ItemsSource).First());
        Assert.Equal(3, headers[0].GetVisualDescendants().OfType<NumericUpDown>().Single().Value);

        var dialogs = form.GetVisualDescendants().OfType<FormDialog>().ToList();
        Assert.Equal(2, dialogs.Count);
        var boxes = dialogs[0].GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(["Eyeris", "Krita", "Spine"], boxes.Select(box => box.GetVisualDescendants().OfType<TextBlock>().First().Text));
        Assert.Equal([true, false, true], boxes.Select(box => box.IsChecked == true));
        Assert.All(dialogs, dialog => Assert.Single(dialog.GetVisualDescendants().OfType<WindowFinder>()));
        Assert.DoesNotContain(dialogs[1].GetVisualDescendants().OfType<CheckBox>(), box => box.IsEffectivelyVisible);
        Assert.Contains(dialogs[1].GetVisualDescendants().OfType<TextBlock>(), text => text.Text == NotInEditViewModel.NoAppsText && text.IsEffectivelyVisible);
    }
}
#endif
