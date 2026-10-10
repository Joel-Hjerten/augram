#if DEBUG
using Augram.App.Components.CommandTree;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>The gallery's Trigger conflict page (gallery rule; Joel, 2026-10-10): a trigger and an input draft with Take it and Swap, and a note without them.</summary>
public sealed class TriggerConflictGalleryTests
{
    [AvaloniaFact]
    public void ThePageShowsBothButtonsForATakenTriggerAndInput_AndNoneForAMissingButton()
    {
        var form = new SectionForm { Screen = Assert.IsType<FormScreen>(TriggerConflictGalleryPage.Page()) };
        new Window { Content = form, Width = 1000, Height = 1400 }.Show();

        var headers = form.GetVisualDescendants().OfType<CommandHeader>().ToList();
        Assert.Equal(["Zoom in", "Pan", "Zoom in"], headers.Select(header => header.NameText));
        Assert.All(headers, header => Assert.NotNull(header.DraftNote));
        Assert.Equal([true, true, false], headers.Select(header => header.CanTakeTrigger));
        Assert.Equal([true, true, false], headers.Select(header => header.CanSwapTrigger));
        Assert.Equal(["Take it from 'Zoom out'", "Take it from 'Orbit'", string.Empty], headers.Select(header => header.TakeText));
        Assert.Equal("Swap: Orbit gets this command's previous input.", headers[1].SwapTip);
        Assert.True(headers[0].GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_SwapTrigger").IsEffectivelyVisible);
    }
}
#endif
