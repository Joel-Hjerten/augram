using Augram.App.Components.GestureGlyph;
using Augram.App.Components.GestureGrid;
using Augram.Core.Gestures;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Gestures;

public sealed class GestureGridTests
{
    [AvaloniaFact]
    public void ShowsOneTilePerItemInTheGivenOrderWithInactiveOnesGreyed()
    {
        var (grid, _) = Show(inactive: "Circle");

        var tiles = grid.TileControls;
        Assert.Equal(StarterGestures.All().Count, tiles.Count);
        Assert.Equal(["C", "Circle", "Down"], tiles.Take(3).Select(tile => tile.NameText));
        var circle = tiles.Single(tile => tile.NameText == "Circle");
        Assert.False(circle.IsActive);
        Assert.Contains(":inactive", circle.Classes);
        var glyph = circle.GetVisualDescendants().OfType<GestureGlyph>().Single();
        Assert.False(glyph.IsActive);
        Assert.NotNull(glyph.Geometry);
        Assert.True(tiles.Single(tile => tile.NameText == "Up").IsActive);
    }

    [AvaloniaFact]
    public void ToolbarButtonsRaiseTheirActions()
    {
        var (grid, actions) = Show();
        var buttons = grid.GetVisualDescendants().OfType<Button>().ToDictionary(button => (string)button.Content!);

        Simulate(buttons["Add new gesture…"]);
        Simulate(buttons["Import from StrokesPlus.net…"]);

        Assert.Equal([GestureGridAction.New, GestureGridAction.Import], actions.Select(action => action.Action));
        Assert.All(actions, action => Assert.Null(action.Tile));
    }

    [AvaloniaFact]
    public void RenameEditsInPlaceAndRaisesRenameWithTheTile()
    {
        var (grid, actions) = Show();
        var circle = StarterGestures.IdFor("Circle");
        grid.Select(circle);

        grid.BeginRename();
        var tile = grid.TileControls.Single(t => t.Item!.Id == circle);
        Assert.True(tile.IsEditing);
        var editor = tile.GetVisualDescendants().OfType<TextBox>().Single();
        editor.Text = "  Loop ";
        editor.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = InputElementKeyDown(), Key = Avalonia.Input.Key.Enter });

        var action = Assert.Single(actions);
        Assert.Equal(GestureGridAction.Rename, action.Action);
        Assert.Equal(circle, action.Tile!.Id);
        Assert.Equal("Loop", action.Name);
        Assert.False(tile.IsEditing);
    }

    [AvaloniaFact]
    public void SelectionSurvivesARebuildAndKeyActionsCarryTheSelectedTile()
    {
        var (grid, actions) = Show();
        var window = (Window)TopLevel.GetTopLevel(grid)!;
        var up = StarterGestures.IdFor("Up");
        grid.Select(up);

        grid.Tiles = grid.Tiles.Where(tile => tile.Name != "Down").ToList();
        FocusSelectedTile(grid);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);

        Assert.Equal(up, grid.SelectedTile!.Id);
        var action = Assert.Single(actions);
        Assert.Equal(GestureGridAction.Delete, action.Action);
        Assert.Equal(up, action.Tile!.Id);
    }

    [AvaloniaFact]
    public void KeyBindingsStandDownWhileANameIsBeingEdited()
    {
        var (grid, actions) = Show();
        var window = (Window)TopLevel.GetTopLevel(grid)!;
        grid.Select(StarterGestures.IdFor("Up"));
        FocusSelectedTile(grid);

        window.KeyPressQwerty(GestureGridKeymap.Rename == "F2" ? PhysicalKey.F2 : PhysicalKey.Enter, RawInputModifiers.None);
        Assert.True(grid.IsEditing);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);

        Assert.Empty(actions);
        Assert.True(grid.IsEditing);
    }

    private static Avalonia.Interactivity.RoutedEvent<Avalonia.Input.KeyEventArgs> InputElementKeyDown() => Avalonia.Input.InputElement.KeyDownEvent;

    private static void Simulate(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    /// <summary>What a click on a tile does: the ListBoxItem wrapping it takes keyboard focus.</summary>
    private static void FocusSelectedTile(GestureGrid grid)
    {
        var list = grid.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.True(list.ContainerFromItem(list.SelectedItem!)!.Focus());
    }

    private static (GestureGrid Grid, List<GestureGridActionEventArgs> Actions) Show(string? inactive = null)
    {
        var tiles = StarterGestures.All()
            .Select(gesture => gesture.Name == inactive ? gesture with { IsActive = false } : gesture)
            .OrderBy(gesture => gesture.Name, GestureRules.NameComparer)
            .Select(GestureTileItem.From)
            .ToList();
        var actions = new List<GestureGridActionEventArgs>();
        var grid = new GestureGrid { Tiles = tiles };
        grid.ActionRequested += (_, e) => actions.Add(e);
        var window = new Window { Content = grid, Width = 800, Height = 600 };
        window.Show();
        return (grid, actions);
    }
}
