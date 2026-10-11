#if DEBUG
using Augram.App.Components.CommandsWorkbench;
using Augram.App.Components.CommandTree;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The command column scrolls as one, as a form does (Joel, 0.11.0: the header stayed put while only the steps scrolled), and
/// starts at the top for another command, never on an edit of the same one.
/// </summary>
public sealed class WorkbenchScrollTests
{
    [AvaloniaFact]
    public void TheHeaderAndTheStepsScrollTogether_AndAnotherCommandStartsAtTheTop()
    {
        var bench = Assert.IsType<CommandsWorkbench>(Assert.IsType<ComponentScreen>(CommandGalleryPages.HoldRemapsPage()).Build());
        var window = new Window { Content = bench, Width = 1200, Height = 420 };
        window.Show();
        window.UpdateLayout();

        var scroll = bench.CommandScrollPart!;
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height, "the header and the open step should not fit in 420 px");
        Assert.Contains(bench.HeaderPart!, scroll.GetVisualDescendants());
        Assert.Contains(bench.StepsPart!, scroll.GetVisualDescendants());

        var bottom = scroll.Extent.Height - scroll.Viewport.Height;
        scroll.Offset = new Vector(0, bottom);
        bench.SelectedCommand = bench.SelectedCommand! with { };
        window.UpdateLayout();
        Assert.Equal(bottom, scroll.Offset.Y);

        var pan = bench.TreePart!.Rows.OfType<CommandRow>().Single(row => row.NameText == "Pan").Item!;
        bench.SelectedCommandId = pan.Id;
        window.UpdateLayout();
        Assert.Equal(0, scroll.Offset.Y);
    }
}
#endif
