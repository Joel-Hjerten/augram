#if DEBUG
using Augram.App.Components.SectionForm;
using Augram.App.Components.StepList;
using Augram.App.Components.StepTypePicker;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The gallery's StepTypePicker and StepList pages (gallery rule; Joel, 0.11.3): the picker with nothing refused, Remap greyed
/// for an ordinary command, Remap offered under a hold remap, everything greyed for Magnifier; and Magnifier's "New step…" disabled.
/// </summary>
public sealed class StepTypePickerGalleryTests
{
    private const string OnlyStep = "A Remap step is a command's only step.";

    [AvaloniaFact]
    public void ThePickerPage_GreysRemapForAnOrdinaryCommand_AndEverythingForMagnifier()
    {
        var form = new SectionForm { Screen = Assert.IsType<FormScreen>(CommandGalleryPages.StepTypePickerPage()) };
        new Window { Content = form, Width = 1000, Height = 2600 }.Show();

        var pickers = form.GetVisualDescendants().OfType<StepTypePicker>().ToList();

        Assert.Equal(4, pickers.Count);
        Assert.All(pickers, picker => Assert.Equal(12, picker.Listed.Count));
        Assert.Equal([12, 11, 12, 0], pickers.Select(picker => picker.Offered.Count));
        var remap = pickers[1].Entries.OfType<Button>().Single(button => button.Tag == RemapStepType.Instance);
        Assert.False(remap.IsEnabled);
        Assert.Equal(StepOffer.NotHere(RemapStepType.Instance), ToolTip.GetTip(remap));
        Assert.All(pickers[3].Entries.OfType<Button>(), button => Assert.Equal(OnlyStep, ToolTip.GetTip(button)));
    }

    [AvaloniaFact]
    public void TheStepListPage_ShowsMagnifiersNewStepDisabled_WithWhy()
    {
        var form = new SectionForm { Screen = Assert.IsType<FormScreen>(CommandGalleryPages.StepListPage()) };
        new Window { Content = form, Width = 1000, Height = 1600 }.Show();

        var lists = form.GetVisualDescendants().OfType<StepList>().ToList();

        Assert.Equal([true, false, false], lists.Select(list => list.CanAddStep));
        Assert.Equal([null, null, OnlyStep], lists.Select(list => list.NewStepRefusal));
        var button = lists[2].GetVisualDescendants().OfType<Button>().Single(part => part.Name == "PART_New");
        Assert.False(button.IsEnabled);
        Assert.Equal(OnlyStep, ToolTip.GetTip(button));
    }
}
#endif
