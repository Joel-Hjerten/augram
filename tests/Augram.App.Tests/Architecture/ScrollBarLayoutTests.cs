using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// A list's vertical scroll bar sits beside its rows, never over them (Joel, 2026-10-09): the Fluent scroll bar auto-hides
/// over the content and widens on hover, and on the Commands tab it covered every row's active check box. The theme
/// turns auto-hide off app-wide, which gives the bar its own column.
/// </summary>
public sealed class ScrollBarLayoutTests
{
    [AvaloniaFact]
    public void AListsScrollBarSitsBesideItsRows()
    {
        var list = new ListBox { ItemsSource = Enumerable.Range(1, 60).Select(row => $"Row {row}").ToList() };
        var window = new Window { Content = list, Width = 320, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rows = list.GetVisualDescendants().OfType<ScrollContentPresenter>().First();
        var bar = list.GetVisualDescendants().OfType<ScrollBar>().First(scrollBar => scrollBar.Orientation == Orientation.Vertical);
        var rowsRight = rows.TranslatePoint(new Point(rows.Bounds.Width, 0), window)!.Value.X;
        var barLeft = bar.TranslatePoint(new Point(0, 0), window)!.Value.X;
        window.Close();

        Assert.True(bar.IsVisible);
        Assert.True(rowsRight <= barLeft + 0.5, $"the rows end at {rowsRight:0.#} px but the scroll bar starts at {barLeft:0.#} px");
    }
}
