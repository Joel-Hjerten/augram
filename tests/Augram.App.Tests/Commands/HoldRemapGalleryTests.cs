#if DEBUG
using Augram.App.Components.CommandsWorkbench;
using Augram.App.Components.CommandTree;
using Augram.App.Components.Steps.Remap;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Augram.Core.Steps.Remap;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>The gallery's Hold remaps page (gallery rule, plan 0002 step 4): Blender's Space open, Orbit selected with its Input and its Remap step's form.</summary>
public sealed class HoldRemapGalleryTests
{
    [AvaloniaFact]
    public void ThePageShowsBlendersSpaceHoldRemapWithOrbitSelected()
    {
        var bench = Assert.IsType<CommandsWorkbench>(Assert.IsType<ComponentScreen>(CommandGalleryPages.HoldRemapsPage()).Build());
        new Window { Content = bench, Width = 1400, Height = 900 }.Show();

        var tree = bench.TreePart!;
        Assert.Equal(["Blender", "Space"], tree.Rows.OfType<SectionRow>().SkipWhile(row => row.NameText != "Blender").Take(2).Select(row => row.NameText));
        Assert.Equal(["Undo", "Grab", "Orbit", "Pan", "Zoom both"], tree.Rows.OfType<CommandRow>().Select(row => row.NameText));
        Assert.Equal("Orbit", bench.SelectedCommand!.Name);
        Assert.True(bench.HeaderPart!.IsInputCommand);
        Assert.True(bench.IsUnderHoldRemap);
        Assert.True(bench.StepsPart!.UnderHoldRemap);
        var row = Assert.Single(bench.StepsPart.Rows);
        Assert.True(row.IsExpanded);
        Assert.IsType<RemapStep>(row.Item!.Step.Step);
        Assert.Contains(row.Form!.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == RemapStepForm.Note);
    }
}
#endif
