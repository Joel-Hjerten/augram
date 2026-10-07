using Augram.App.Components.GestureGrid;
using Augram.App.Components.GesturePicker;
using Augram.Core.Gestures;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class GesturePickerTests
{
    [AvaloniaFact]
    public void ShowsATilePerGestureAndOkNeedsASelection()
    {
        var (picker, results, _) = Show();
        var buttons = Buttons(picker);

        Assert.Equal(StarterGestures.All().Count, picker.TileControls.Count);
        Assert.False(picker.HasSelection);
        Assert.False(buttons["OK"].IsEnabled);

        picker.Select(StarterGestures.IdFor("Circle"));

        Assert.True(picker.HasSelection);
        Assert.True(buttons["OK"].IsEnabled);
        Click(buttons["OK"]);
        Assert.Equal(GesturePickerResult.Selected(StarterGestures.IdFor("Circle")), Assert.Single(results));
    }

    [AvaloniaFact]
    public void TheOtherButtonsAnswerNoGestureCancelAndNewGesture()
    {
        var (picker, results, newRequests) = Show();
        var buttons = Buttons(picker);

        Click(buttons["No Gesture"]);
        Click(buttons["Cancel"]);
        Click(buttons["New Gesture…"]);

        Assert.Equal([GesturePickerOutcome.NoGesture, GesturePickerOutcome.Cancelled], results.Select(result => result.Outcome));
        Assert.Single(newRequests);
    }

    [AvaloniaFact]
    public void SelectionSurvivesATileRebuildByIdAndAListClickSelects()
    {
        var (picker, _, _) = Show();
        var up = StarterGestures.IdFor("Up");
        picker.Select(up);

        picker.Tiles = [.. picker.Tiles, GestureTileItem.From(new Gesture(GestureId.New(), "Late", IsActive: true, [new GestureSample([new(0, 0), new(10, 10)])]))];

        Assert.Equal(up, picker.SelectedId);
        var list = picker.GetVisualDescendants().OfType<ListBox>().Single();
        list.SelectedItem = picker.TileControls.Single(tile => tile.NameText == "Late");
        Assert.Equal("Late", picker.Tiles.Single(tile => tile.Id == picker.SelectedId).Name);
    }

    private static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    private static Dictionary<string, Button> Buttons(GesturePicker picker)
        => picker.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("toolbar")).ToDictionary(button => (string)button.Content!);

    private static (GesturePicker Picker, List<GesturePickerResult> Results, List<EventArgs> NewRequests) Show()
    {
        var results = new List<GesturePickerResult>();
        var picker = new GesturePicker { Tiles = [.. StarterGestures.All().Select(GestureTileItem.From)] };
        var newRequests = new List<EventArgs>();
        picker.Closed += (_, result) => results.Add(result);
        picker.NewGestureRequested += (_, e) => newRequests.Add(e);
        var window = new Window { Content = picker, Width = 600, Height = 500 };
        window.Show();
        return (picker, results, newRequests);
    }
}
