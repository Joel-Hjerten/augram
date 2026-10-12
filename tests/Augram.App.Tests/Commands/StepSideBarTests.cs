#if DEBUG
using Augram.App.Components.CommandHeader;
using Augram.App.Components.CommandsWorkbench;
using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.Declarations;
using Augram.App.DevGallery;
using Augram.Core.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The "Steps for" row is the Steps card's first row, above the steps, as "Fields for" is of the App identification card
/// (Joel, 0.14.3), not a row of the command header; a step list with nothing in its slot draws no such row.
/// </summary>
public sealed class StepSideBarTests
{
    [AvaloniaFact]
    public void StepsForIsTheFirstRowOfTheStepsCard_AndItsSwitchStillAsks()
    {
        var bench = Assert.IsType<CommandsWorkbench>(Assert.IsType<ComponentScreen>(CommandGalleryPages.AppsWorkbenchPage()).Build());
        var window = new Window { Content = bench, Width = 1200, Height = 900 };
        window.Show();
        window.UpdateLayout();

        var bar = bench.StepSidePart!;
        var steps = bench.StepsPart!;
        Assert.True(bar.IsEffectivelyVisible);
        Assert.True(bar.HasStepSide);
        Assert.Contains(bar, steps.GetVisualDescendants());
        Assert.DoesNotContain(bar, bench.HeaderPart!.GetVisualDescendants());
        Assert.DoesNotContain(bench.HeaderPart!.GetVisualDescendants().OfType<RadioButton>(), button => button.Name?.StartsWith("PART_StepsFor", StringComparison.Ordinal) == true);
        var rows = steps.GetVisualDescendants().OfType<ListBox>().Single(list => list.Name == "PART_Rows");
        Assert.True(bar.TranslatePoint(new Point(0, bar.Bounds.Height), rows)!.Value.Y <= 0, "the Steps for row sits above the step rows");

        var asked = new List<CommandTreeActionEventArgs>();
        bench.TreeActionRequested += (_, e) => asked.Add(e);
        var other = bar.Item!.StepSide!.Shown.Other();
        bar.GetVisualDescendants().OfType<RadioButton>().Single(button => button.Name == (other == HostPlatform.Windows ? "PART_StepsForWindows" : "PART_StepsForMac")).IsChecked = true;
        var ask = Assert.Single(asked);
        Assert.Equal(CommandTreeAction.ShowStepsFor, ask.Action);
        Assert.Equal(other, ask.Platform);
    }

    [AvaloniaFact]
    public void AStepListWithAnEmptySlotDrawsNoFirstRow()
    {
        var list = new StepList { HasCommand = true };
        var window = new Window { Content = list, Width = 600, Height = 400 };
        window.Show();
        window.UpdateLayout();

        var slot = list.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "PART_CardHeader");
        Assert.False(slot.IsVisible);
        Assert.Empty(list.GetVisualDescendants().OfType<StepSideBar>());
    }
}
#endif
