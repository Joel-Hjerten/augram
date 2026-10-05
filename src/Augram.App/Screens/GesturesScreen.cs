using Augram.App.Components.GestureGrid;
using Augram.App.Declarations;
using Augram.App.ViewModels;
using Avalonia.Data;

namespace Augram.App.Screens;

/// <summary>
/// The Gestures tab (F3, F4, F5a): one <see cref="GestureGrid"/> filling the tab, bound to
/// <see cref="GesturesViewModel"/>. The grid raises intents; the view model turns them into
/// library calls. The training popup and the import dialog are opened by the view model's presenters.
/// </summary>
public static class GesturesScreen
{
    /// <summary>
    /// Step 6 placeholder, kept only so <c>AppNavigation</c> compiles until the lead swaps its Gestures
    /// entry for <c>GesturesModule.NavEntry(services)</c>; delete this overload in that change.
    /// </summary>
    public static ScreenDeclaration Declare() =>
        new TextScreen("Gestures", "The Gestures grid is wired through GesturesModule.NavEntry; this placeholder means the navigation entry has not been swapped yet.");

    public static ScreenDeclaration Declare(GesturesViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new ComponentScreen("Gestures", () => Build(vm));
    }

    private static GestureGrid Build(GesturesViewModel vm)
    {
        var grid = new GestureGrid { DataContext = vm };
        grid.Bind(GestureGrid.TilesProperty, new Binding(nameof(GesturesViewModel.Tiles)));
        grid.Bind(GestureGrid.CanUndoProperty, new Binding(nameof(GesturesViewModel.CanUndo)));
        grid.Bind(GestureGrid.CanRedoProperty, new Binding(nameof(GesturesViewModel.CanRedo)));
        grid.Bind(GestureGrid.MessageProperty, new Binding(nameof(GesturesViewModel.Message)));
        grid.Bind(GestureGrid.DiagnosticProperty, new Binding(nameof(GesturesViewModel.Diagnostic)));
        grid.ActionRequested += (_, e) => vm.Handle(e);
        return grid;
    }
}
